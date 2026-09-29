using MassTransit;

namespace IntegrationTests;

/// <summary>
/// Records how far a message travelled on its way from the broker to the consumer.
/// </summary>
/// <remarks>
/// <para>
/// The guard proved routing is correct and the consumer is never entered, which left one question:
/// <b>how far does the message actually get?</b> These observers bracket the path so the answer
/// is measured rather than guessed.
/// </para>
/// <para>
/// Each stage is a distinct boundary, and the first one that does not fire localises the fault:
/// <list type="number">
/// <item><description>EndpointReady - the receive endpoint reported ready, with a consumer attached.</description></item>
/// <item><description>PreReceive - the transport delivered the message off the broker.</description></item>
/// <item><description>ConsumeEntered - the consumer body executed.</description></item>
/// </list>
/// </para>
/// <para>
/// Properties are <c>internal set</c> (not <c>private set</c>) because the observer types are
/// separate classes that write them. Static and process-scoped, so results are reset per test.
/// </para>
/// </remarks>
public static class DeliveryTrace
{
    public static bool EndpointReady { get; internal set; }

    public static bool EndpointReallyStarted { get; internal set; }

    public static string? ReadyEndpointInputAddress { get; internal set; }

    public static bool PreReceive { get; internal set; }

    public static bool PostReceive { get; internal set; }

    public static int PreReceiveCount { get; internal set; }

    public static string? PreReceiveInputAddress { get; internal set; }

    public static bool PreReceiveRedelivered { get; internal set; }

    /// <summary>Transport- or endpoint-level fault reported by MassTransit, if any.</summary>
    public static string? EndpointFault { get; internal set; }

    public static string? ReceiveFault { get; internal set; }

    // ---- Send-side observations (Step 1a) ----
    //
    // Every prior failure reported that the message did not ARRIVE, which cannot distinguish "sent
    // to the wrong exchange" from "sent correctly and lost in transit". Those are different bugs
    // with different fixes, and the send side had never been observed directly. PreSend reports the
    // address the transport is actually about to use, which names the exchange outright.

    /// <summary>Number of messages observed leaving through the transport.</summary>
    public static int PreSendCount { get; internal set; }

    /// <summary>The address the transport is publishing to, e.g. a RabbitMQ exchange URI.</summary>
    public static string? PreSendAddress { get; internal set; }

    /// <summary>
    /// The STATIC generic type the send was dispatched as - i.e. the type argument of the
    /// Send&lt;T&gt; that MassTransit actually invoked. This is the erasure signal: if a concrete
    /// event goes out as its abstract base, this reads the base type.
    /// </summary>
    public static string? PreSendStaticType { get; internal set; }

    /// <summary>The RUNTIME type of the message body actually being sent.</summary>
    public static string? PreSendRuntimeType { get; internal set; }

    public static string? PostSendAddress { get; internal set; }

    public static string? SendFaultDetail { get; internal set; }

    public static void ResetObservations()
    {
        EndpointReady = false;
        EndpointReallyStarted = false;
        ReadyEndpointInputAddress = null;
        PreReceive = false;
        PostReceive = false;
        PreReceiveCount = 0;
        PreReceiveInputAddress = null;
        PreReceiveRedelivered = false;
        EndpointFault = null;
        ReceiveFault = null;
        PreSendCount = 0;
        PreSendAddress = null;
        PreSendStaticType = null;
        PreSendRuntimeType = null;
        PostSendAddress = null;
        SendFaultDetail = null;
    }

    /// <summary>
    /// Human-readable localisation of where the path stopped. Used in the guard's failure message.
    /// </summary>
    public static string Localise()
    {
        // The send side is checked FIRST. If nothing was ever handed to the transport, then the
        // receive-side stages below are describing an endpoint that was never fed, and reporting
        // "the broker delivered nothing" would be a conclusion about the wrong component.
        if (PreSendCount == 0)
        {
            return "STAGE -1 (SEND) FAILED: nothing reached the transport at all. The publish never " +
                   $"produced a send, so no receive-side stage below is meaningful. " +
                   $"SendFault={(SendFaultDetail ?? "none")}.";
        }

        var sendSummary =
            $"Sent as static type '{PreSendStaticType ?? "(null)"}' " +
            $"(runtime body '{PreSendRuntimeType ?? "(null)"}') to address '{PreSendAddress ?? "(null)"}'" +
            (PostSendAddress is null ? "; no PostSend was observed" : $"; PostSend address '{PostSendAddress}'") +
            (SendFaultDetail is null ? "" : $"; SendFault={SendFaultDetail}");

        if (PreSendStaticType is not null && PreSendRuntimeType is not null
            && PreSendStaticType != PreSendRuntimeType)
        {
            return "STAGE -1 (SEND) FAILED: TYPE ERASURE. " + sendSummary +
                   ". The static and runtime types differ, so the exchange was chosen from the " +
                   "static type - this is the original defect reproducing in the harness.";
        }

        if (EndpointFault is not null)
        {
            return $"Stage 0 FAILED: the receive endpoint FAULTED before it could consume: {EndpointFault}";
        }

        if (!EndpointReady)
        {
            return "Stage 0 FAILED: the receive endpoint never reported Ready. The broker shows a consumer, " +
                   "so the endpoint was declared but never reached the ready state - nothing could be consumed. " +
                   sendSummary;
        }

        if (!EndpointReallyStarted)
        {
            return "Stage 0b FAILED: the endpoint reported Ready but IsStarted=false, i.e. a 'fake-ready' endpoint " +
                   "that does not auto-start. Nothing is actually consuming.";
        }

        if (PreReceiveCount == 0)
        {
            return "Stage 1 FAILED: the message WAS handed to the transport and confirmed sent, but the " +
                   $"consumer never received it. {sendSummary}. " +
                   $"Endpoint input address = {ReadyEndpointInputAddress ?? "(none)"}. " +
                   $"Expected queue name = {TestBroker.QueueName}. " +
                   $"EndpointFault={(EndpointFault ?? "none")} ReceiveFault={(ReceiveFault ?? "none")}. " +
                   "If the send address above is an exchange this run's queue is NOT bound to, the " +
                   "message is discarded by the broker - that is routing, not transport failure. " +
                   "If it IS bound, the fault is in the binding or in the endpoint's own consumer.";
        }

        if (!TestDomainEventConsumer.ConsumeEntered)
        {
            return $"Stage 2 FAILED: the transport DID deliver {PreReceiveCount} message(s) off the broker " +
                   $"(via {PreReceiveInputAddress}, redelivered={PreReceiveRedelivered}), but the consumer body " +
                   "never ran. The fault is in the receive pipeline between transport and consumer " +
                   "(filtering, message-type matching, or a skipped message).";
        }

        return $"Delivery completed: endpoint ready, {PreReceiveCount} message(s) delivered, consumer invoked.";
    }
}

/// <summary>Endpoint lifecycle observer. Records whether the endpoint actually started.</summary>
public sealed class ReadyObserver : IReceiveEndpointObserver
{
    public Task Ready(ReceiveEndpointReady ready)
    {
        DeliveryTrace.EndpointReady = true;
        DeliveryTrace.EndpointReallyStarted = ready.IsStarted;
        DeliveryTrace.ReadyEndpointInputAddress = ready.InputAddress?.ToString();
        return Task.CompletedTask;
    }

    public Task Stopping(ReceiveEndpointStopping stopping) => Task.CompletedTask;

    public Task Completed(ReceiveEndpointCompleted completed) => Task.CompletedTask;

    public Task Faulted(ReceiveEndpointFaulted faulted)
    {
        DeliveryTrace.EndpointFault = faulted.Exception?.ToString();
        return Task.CompletedTask;
    }
}

/// <summary>Message observer. Records whether the transport delivered anything off the broker.</summary>
public sealed class PrePostReceiveObserver : IReceiveObserver
{
    public Task PreReceive(ReceiveContext context)
    {
        DeliveryTrace.PreReceive = true;
        DeliveryTrace.PreReceiveCount++;
        DeliveryTrace.PreReceiveInputAddress = context.InputAddress?.ToString();
        DeliveryTrace.PreReceiveRedelivered = context.Redelivered;
        return Task.CompletedTask;
    }

    public Task PostReceive(ReceiveContext context)
    {
        DeliveryTrace.PostReceive = true;
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
        where T : class
        => Task.CompletedTask;

    public Task ConsumeFault<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception)
        where T : class
    {
        DeliveryTrace.ReceiveFault = exception.ToString();
        return Task.CompletedTask;
    }

    public Task ReceiveFault(ReceiveContext context, Exception exception)
    {
        DeliveryTrace.ReceiveFault = exception.ToString();
        return Task.CompletedTask;
    }
}
/// <summary>
/// Send observer. Records the address the transport is about to publish to, plus the static and
/// runtime types of the message.
/// </summary>
/// <remarks>
/// This is the observation the investigation was missing. Every previous failure said only that the
/// message did not arrive, which cannot tell "published to an exchange nothing is bound to" apart from
/// "published correctly and lost in transit". <c>PreSend</c> receives the resolved
/// <c>SendContext</c>, so <c>DestinationAddress</c> names the exchange outright and
/// <c>typeof(T)</c> is the type argument MassTransit actually dispatched with - which is precisely
/// the value the original defect got wrong.
/// </remarks>
public sealed class SendAddressObserver : ISendObserver
{
    public Task PreSend<T>(SendContext<T> context) where T : class
    {
        DeliveryTrace.PreSendCount++;
        DeliveryTrace.PreSendAddress = context.DestinationAddress?.ToString();
        DeliveryTrace.PreSendStaticType = typeof(T).FullName;
        DeliveryTrace.PreSendRuntimeType = context.Message?.GetType().FullName;
        return Task.CompletedTask;
    }

    public Task PostSend<T>(SendContext<T> context) where T : class
    {
        DeliveryTrace.PostSendAddress = context.DestinationAddress?.ToString();
        return Task.CompletedTask;
    }

    public Task SendFault<T>(SendContext<T> context, Exception exception) where T : class
    {
        DeliveryTrace.SendFaultDetail = exception.ToString();
        return Task.CompletedTask;
    }
}
