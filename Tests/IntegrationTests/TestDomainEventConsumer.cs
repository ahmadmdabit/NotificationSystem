using MassTransit;

using TestDoubles.Stubs;

namespace IntegrationTests;

/// <summary>
/// Records consumed <see cref="TestDomainEvent"/>s so a delivery can be awaited rather than polled.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not injected into the consumer.</b> MassTransit's
/// <c>ReceiveEndpointConfigurator.Consumer&lt;TConsumer&gt;()</c> constrains
/// <c>TConsumer</c> to a <i>parameterless</i> constructor (CS0310), so a consumer cannot take
/// constructor dependencies through that API. An earlier version made the consumer a DI singleton
/// holding the <see cref="TaskCompletionSource{TResult}"/> and awaited that instance directly. It
/// never completed - the message was delivered and the queue drained to zero, but MassTransit
/// invoked its own consumer instance rather than the registered one. The consumer is now a
/// parameterless shell that forwards to this recorder, so instance identity no longer matters.
/// </para>
/// <para>
/// <b>Matching on payload, not just arrival.</b> Each test waits for a specific message body, so a
/// leftover message from a previous run cannot satisfy the wait. That removes the need to purge the
/// queue before each test, and with it a whole class of flakiness and a privileged management-API
/// call.
/// </para>
/// </remarks>
public sealed class TestDomainEventRecorder
{
    private readonly List<TestDomainEvent?> received = [];
    private readonly Lock gate = new();

    /// <summary>
    /// Waits for a consumed <see cref="TestDomainEvent"/> whose <c>Data</c> equals
    /// <paramref name="expectedData"/>, or throws on <paramref name="timeout"/>.
    /// </summary>
    public async Task<TestDomainEvent> WaitAsync(string expectedData, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            lock (gate)
            {
                var match = received.FirstOrDefault(e => e?.Data == expectedData);
                if (match is not null)
                {
                    return match;
                }
            }

            await Task.Delay(100);
        }

        string seen;
        lock (gate)
        {
            seen = received.Count == 0
                ? "nothing was consumed"
                : $"consumed: {string.Join(", ", received.Select(e => e is null ? "<null body>" : $"'{e.Data}'"))}";
        }

        var state = await TestBroker.DescribeQueueStateAsync(
            TestBroker.QueueName, typeof(TestDomainEvent), CancellationToken.None);

        // The decisive observation: was Consume entered at all? This turns "nothing arrived" into
        // a diagnosis, and is the only fact not derivable from broker state or from source.
        var consumer = TestDomainEventConsumer.ConsumeEntered
            ? $"Consume WAS entered for MessageId={TestDomainEventConsumer.LastObservedMessageId} " +
              $"as {TestDomainEventConsumer.LastObservedMessageType ?? "null"} " +
              $"(Data='{TestDomainEventConsumer.LastObservedBody ?? "null"}') - so the broker DID deliver, " +
              "and the fault is deserialization or the payload match, not delivery"
            : "Consume was NEVER entered - the broker accepted the publish but delivered nothing to the " +
              "attached consumer, so the fault is between the bound queue and the AMQP consumer";

        throw new TimeoutException(
            $"No TestDomainEvent with Data='{expectedData}' was consumed within {timeout.TotalSeconds:0.#}s " +
            $"({seen}). {consumer}. Delivery path: {DeliveryTrace.Localise()}. Broker state: {state}. " +
            "ready=0 with consumers=1 means the broker delivered nothing despite an attached consumer; " +
            "consumers=0 means the receive endpoint never started; " +
            "more than one bound queue means a stale or concurrent host is consuming this run's messages.");
    }

    // Null is meaningful, not exceptional: the consumer records what actually arrived, and a null
    // body is precisely the deserialization-failure signal the guard reports. Discarding it here
    // would hide the one observation that separates "never delivered" from "delivered unresolvable".
    internal void Record(TestDomainEvent? message)
    {
        lock (gate)
        {
            received.Add(message);
        }
    }
}

/// <summary>
/// Parameterless consumer that forwards to the shared <see cref="TestDomainEventRecorder"/>.
/// </summary>
/// <remarks>
/// The parameterless constructor is required by
/// <c>ReceiveEndpointConfigurator.Consumer&lt;TConsumer&gt;()</c>. All state therefore lives in
/// <see cref="TestDomainEventRecorder"/>, which is a static so it is reachable without constructor
/// injection. This is a test-only static scoped to a single test process and serialised by
/// <c>[NotInParallel]</c>.
/// </remarks>
public sealed class TestDomainEventConsumer : IConsumer<TestDomainEvent>
{
    public static TestDomainEventRecorder Recorder { get; } = new();

    /// <summary>
    /// Whether <see cref="Consume"/> is entered at all, and with what.
    /// </summary>
    /// <remarks>
    /// The investigation reached the point where every structural explanation was eliminated -
    /// routing provably correct, exchange provably bound, consumer provably attached, queue
    /// provably empty. The one fact never directly observed was whether this method runs.
    /// Recording entry splits the remaining space cleanly:
    /// <list type="bullet">
    /// <item><description>no entry at all -> the broker never delivered;</description></item>
    /// <item><description>entry with a null body -> deserialization;</description></item>
    /// <item><description>entry with a non-matching payload -> the recorder missed it.</description></item>
    /// </list>
    /// This is the observability the guard should have had from the start: every earlier failure
    /// reported only that nothing arrived, which is not a diagnosis.
    /// </remarks>
    public static bool ConsumeEntered { get; private set; }

    public static string? LastObservedMessageType { get; private set; }

    public static Guid? LastObservedMessageId { get; private set; }

    public static string? LastObservedBody { get; private set; }

    /// <summary>Clears the static observations, since they persist across tests in one process.</summary>
    public static void ResetObservations()
    {
        ConsumeEntered = false;
        LastObservedMessageType = null;
        LastObservedMessageId = null;
        LastObservedBody = null;
    }

    public Task Consume(ConsumeContext<TestDomainEvent> context)
    {
        ConsumeEntered = true;
        LastObservedMessageType = context.Message?.GetType().FullName;
        LastObservedMessageId = context.MessageId;
        LastObservedBody = context.Message?.Data;

        Recorder.Record(context.Message);
        return Task.CompletedTask;
    }
}
