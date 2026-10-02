using MassTransit;

using Microsoft.Extensions.DependencyInjection;

using Shared.Domain;
using Shared.Domain.Abstractions;
using Shared.Infrastructure;

using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace IntegrationTests;

/// <summary>
/// Real-broker guard for the generic-type-erasure defect in
/// <see cref="MassTransitDomainEventDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the defect was.</b> The dispatcher published with <c>Publish&lt;T&gt;</c>, and MassTransit
/// derives the exchange name from the <i>static</i> type argument, not from
/// <c>message.GetType()</c>. <c>TransactionBehavior</c> drains a <c>List&lt;DomainEvent&gt;</c>, so
/// <c>T</c> inferred as the base type and every event was published to
/// <c>Shared.Domain:DomainEvent</c> — an exchange with no bound queue. RabbitMQ accepted and
/// discarded the message: no exception, HTTP 200, empty queue, silent consumer.
/// </para>
/// <para>
/// <b>Why this test needs a real broker.</b> A mocked <c>IPublishEndpoint</c> cannot observe
/// exchange naming, so no unit-level test can catch this. The in-memory transport is equally
/// blind. Only a real broker distinguishes "the consumer received it" from "the message went
/// somewhere unbound".
/// </para>
/// <para>
/// Not parallel: every test here shares one broker and one queue name.
/// </para>
/// </remarks>
[NotInParallel]
public class DomainEventDispatchTests
{
    // 45s, not 15s. A solution-wide `dotnet test` runs every test assembly at once, so this
    // process can be CPU-starved on a loaded machine. The round trip is ~350ms when the
    // machine is idle; the budget exists to absorb contention, not to hide a routing fault.
    private static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(45);

    // Topology must exist before the first publish. Bounded and short: this is a correctness gate,
    // not a slack budget, and a slow pass here means the endpoint never attached.
    private static readonly TimeSpan TopologyTimeout = TimeSpan.FromSeconds(30);

    [Before(HookType.Test)]
    public void SetUp()
    {
        TestBroker.EnsureReachable();

        // Consumer observations are static, so they must be cleared per test or a previous test's
        // entry would be misreported as this test's.
        TestDomainEventConsumer.ResetObservations();

        // Delivery observations are static for the same reason.
        DeliveryTrace.ResetObservations();
    }

    /// <summary>
    /// The core guard: a domain event published through a <see cref="DomainEvent"/>-typed
    /// variable must arrive at a consumer bound to its <i>concrete</i> exchange.
    /// </summary>
    [Test]
    public async Task DomainEvent_PublishedThroughBaseTypedVariable_IsConsumed()
    {
        // The cast in the dispatcher is the whole point of this test. Written as
        // PublishAsync(new TestDomainEvent(...)) the compiler would infer T = TestDomainEvent,
        // the exchange would be right, and this test would PASS against the buggy dispatcher.
        // Erasure only occurs through a DomainEvent-typed variable - which is precisely what
        // TransactionBehavior List<DomainEvent> produces in production.
        DomainEvent domainEvent = TestDomainEventFactory.Create("guard");

        var (provider, scope, bus) = await StartBusAsync();
        try
        {
            // Topology must be correct before anything is published.
            await TestBroker.AssertQueueReachableAsync(
                bus.Topology, TestBroker.QueueName, typeof(TestDomainEvent), CancellationToken.None);

            // THE DISCRIMINATING PROBE (rev 6 §31).
            //
            // Two addresses that can disagree:
            //   declared  = what IBusTopology reports the publish exchange to be
            //   publisher = what the live IPublishEndpoint actually resolves at send time
            //
            // PublishEndpointProvider caches send endpoints by typeof(T), so a base-type entry
            // created first would keep routing publishes to Shared.Domain:DomainEvent while the
            // queue is correctly bound to the concrete exchange. Both facts are true at once and
            // the topology assertion above still passes - only the publisher looks in the wrong
            // place. Comparing the two is the only way to see it.
            // Publish once through the BASE-typed dispatcher, exactly as TransactionBehavior does,
            // then observe WHICH queue the broker actually puts it on. The concrete queue's
            // consumer binding is declared, so if the message is delivered there the routing is
            // correct end to end. No counters, no internals, no address comparison - just behaviour.
            //
            // The address-comparison probe from rev 6 §31 was abandoned: the address the publisher
            // resolves is held on internal types (SendEndpoint / ISendTransport), so comparing it
            // would require reflecting into framework internals. This is the same hypothesis tested
            // observationally instead.

            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            await dispatcher.PublishAsync(domainEvent, CancellationToken.None);

            var received = await TestDomainEventConsumer.Recorder.WaitAsync("guard", ConsumeTimeout);

            await Assert.That(received).IsNotNull();
            await Assert.That(received!.Data).IsEqualTo("guard")
                .Because("the concrete event must survive the round trip intact");
        }
        finally
        {
            // The bus is stopped BEFORE the provider is disposed. `await using (provider)` runs
            // disposal first, which tears the bus down underneath StopAsync and leaves the
            // shared queue bound for the next test in this class.
            await bus.StopAsync();
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    /// <summary>
    /// Pins the exact routing error, so a failure says where the message went instead of only
    /// reporting that it never arrived.
    /// </summary>
    /// <remarks>
    /// RabbitMQ aggregates <c>message_stats</c> on an interval rather than per message, so the
    /// counters are read <b>after</b> waiting for the concrete exchange's tally to move. Asserting
    /// an immediate before/after delta of exactly 1 is inherently flaky and would make this test a
    /// second source of false failures.
    /// </remarks>
    [Test]
    public async Task DomainEvent_IsPublishedToConcreteExchange_NotTheBaseTypeExchange()
    {
        var (provider, scope, bus) = await StartBusAsync();
        try
        {
            // DETERMINISTIC assertion - no counters, no sampling, no sleep.
            //
            // Ask the bus which exchange it would publish this RUNTIME type to, then verify that
            // exchange is bound to our queue through the full two-hop topology. This is the exact
            // value the defect got wrong, and reading it needs no publication at all, so the result
            // cannot be timing-dependent.
            //
            // This REPLACES the previous message_stats.publish_out polling. Those counters are
            // aggregated on an interval and lag publication by 200-500ms, so a sample taken around a
            // publish reports stale values and yields false verdicts in both directions. An earlier
            // version of this test "proved" the defect was live from a lagging counter; it was not.
            await TestBroker.AssertPublishAddressBoundAsync(
                bus.Topology,
                TestBroker.QueueName,
                typeof(TestDomainEvent),
                CancellationToken.None);

            // The base type is the erasure TARGET. Nothing may be bound to it, or an erased publish
            // would be silently absorbed and the defect would hide behind a passing test.
            await AssertPublishAddressUnreachableAsync(bus.Topology, typeof(DomainEvent));

            // Round trip through the production dispatcher: the end-to-end proof that the address
            // above is not merely declared but actually delivers.
            DomainEvent domainEvent = TestDomainEventFactory.Create("routing");

            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            await dispatcher.PublishAsync(domainEvent, CancellationToken.None);

            var received = await TestDomainEventConsumer.Recorder.WaitAsync("routing", ConsumeTimeout);

            await Assert.That(received.Data).IsEqualTo("routing")
                .Because("the event must survive the broker round trip intact");
        }
        finally
        {
            await bus.StopAsync();
            await scope.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    /// <summary>
    /// Builds a real RabbitMQ bus using the production dispatcher, mirroring how each
    /// service registers messaging.
    /// </summary>
    /// <summary>
    /// Asserts nothing is bound to <paramref name="messageType"/>'s publish exchange.
    /// </summary>
    /// <remarks>
    /// Used for the base <see cref="DomainEvent"/> type. If a queue were bound there, a publish whose
    /// generic type had been erased would be consumed rather than discarded, and the guard could not
    /// tell a correct dispatcher from a broken one.
    /// </remarks>
    private static async Task AssertPublishAddressUnreachableAsync(IBusTopology topology, Type messageType)
    {
        if (!topology.TryGetPublishAddress(messageType, out var publishAddress))
        {
            // No publish address at all is the strongest form of unreachable: nothing can be routed there.
            return;
        }

        var exchange = publishAddress.AbsolutePath.Trim('/');
        var bound = await TestBroker.QueuesBoundToExchangeAsync(exchange, CancellationToken.None);

        await Assert.That(bound).IsEmpty()
            .Because(
                $"{messageType.FullName} is the type-erasure target, so its publish exchange " +
                $"'{exchange}' must have NO queue reachable from it. A bound queue would consume an " +
                "erased message and mask the defect" +
                (bound.Count > 0 ? $". Found: {string.Join(", ", bound)}" : string.Empty));
    }

    private static async Task<(ServiceProvider Provider, AsyncServiceScope Scope, IBusControl Bus)>
        StartBusAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri($"rabbitmq://{TestBroker.BrokerHost}:{TestBroker.BrokerPort}"), h =>
                {
                    h.Username(TestBroker.BrokerUser);
                    h.Password(TestBroker.BrokerPassword);
                });

                // Queue name is unique per process, so a leaked or concurrent run cannot consume
                // this run's messages. The binding still targets the concrete message type's
                // publish exchange.
                //
                // AutoDelete is deliberately NOT set: an auto-delete queue is removed whenever the
                // last consumer disconnects, including the brief window during bus startup. A
                // message published in that window reaches an exchange with no binding and is
                // discarded, which made this test fail intermittently.
                cfg.ReceiveEndpoint(TestBroker.QueueName, e => e.Consumer<TestDomainEventConsumer>());
            });
        });

        // The component under guard, registered exactly as each service does.
        services.AddScoped<IDomainEventDispatcher, MassTransitDomainEventDispatcher>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        var bus = provider.GetRequiredService<IBusControl>();

        // Observers bracket the delivery path so a failure localises to a stage instead of only
        // reporting that nothing arrived. See DeliveryTrace for the stage boundaries.
        bus.ConnectReceiveEndpointObserver(new ReadyObserver());
        bus.ConnectReceiveObserver(new PrePostReceiveObserver());

        // The send-side counterpart. Connected to the bus (not the scoped endpoint) because the
        // transport is what resolves the final address, so this observes the real publish target
        // regardless of which IPublishEndpoint instance the dispatcher holds.
        bus.ConnectSendObserver(new SendAddressObserver());

        await bus.StartAsync();

        // Deterministic topology gate. StartAsync resolves when the bus is started, but the queue
        // declaration and its binding are broker round trips, so a publish issued immediately after
        // can beat the binding into existence and be discarded. The original defect was exactly that
        // shape - accepted by the exchange, delivered nowhere - so a harness that races its own
        // topology would manufacture the failure it exists to detect.
        await TestBroker.WaitForConsumerAttachedAsync(TestBroker.QueueName, TopologyTimeout);

        // The scope is created AFTER the bus has started, and that ordering is load-bearing.
        //
        // MassTransit registers IPublishEndpoint as SCOPED. The scoped instance is the lazily
        // constructed BusScopedBusContext.PublishEndpoint, which captures its provider on FIRST
        // ACCESS and builds `new PublishEndpoint(bus, provider)` from it. Creating the scope before
        // StartAsync - and resolving the dispatcher inside it - therefore hands the dispatcher an
        // endpoint bound to a bus that was not yet running, so the publish is accepted and delivered
        // nowhere. That was the cause of every failure in this harness: the topology was correct,
        // the consumer was attached, and nothing was ever delivered.
        //
        // Production never hits this: the dispatcher is resolved per request, long after the host
        // has started.
        var scope = provider.CreateAsyncScope();

        return (provider, scope, bus);
    }
}
