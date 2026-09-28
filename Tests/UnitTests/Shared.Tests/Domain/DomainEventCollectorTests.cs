using Shared.Domain;

using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Domain;

public class DomainEventCollectorTests
{
    [Before(HookType.Test)]
    public void SetUp()
    {
        DomainEventCollector.Clear();
    }

    [After(HookType.Test)]
    public void TearDown()
    {
        DomainEventCollector.Clear();
    }

    [Test]
    public async Task Add_WhenEventIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() => DomainEventCollector.Add(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Add_SingleEvent_IsRetainedAndDrained()
    {
        var evt = TestDomainEventFactory.Create();

        DomainEventCollector.Add(evt);

        var drained = DomainEventCollector.Drain();
        await Assert.That(drained.Count()).IsEqualTo(1);
        await Assert.That(drained[0]).IsEqualTo(evt);
    }

    [Test]
    public async Task AddRange_WhenEventsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() => DomainEventCollector.AddRange(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task AddRange_MultipleEvents_AreRetainedInOrder()
    {
        var e1 = TestDomainEventFactory.Create("first");
        var e2 = TestDomainEventFactory.Create("second");
        var e3 = TestDomainEventFactory.Create("third");

        DomainEventCollector.AddRange([e1, e2, e3]);

        var drained = DomainEventCollector.Drain();
        await Assert.That(drained.Count()).IsEqualTo(3);
        await Assert.That(drained[0]).IsEqualTo(e1);
        await Assert.That(drained[1]).IsEqualTo(e2);
        await Assert.That(drained[2]).IsEqualTo(e3);
    }

    [Test]
    public async Task Drain_WhenEmpty_ReturnsEmptyList()
    {
        var drained = DomainEventCollector.Drain();

        await Assert.That(drained).IsEmpty();
    }

    [Test]
    public async Task Drain_EmptiesCollector_SubsequentDrainReturnsEmpty()
    {
        DomainEventCollector.Add(TestDomainEventFactory.Create());

        _ = DomainEventCollector.Drain();
        var second = DomainEventCollector.Drain();

        await Assert.That(second).IsEmpty();
    }

    [Test]
    public async Task Clear_DiscardsPendingEventsWithoutThrowing()
    {
        DomainEventCollector.Add(TestDomainEventFactory.Create());
        DomainEventCollector.Add(TestDomainEventFactory.Create());

        // Clear() has side effects (clears AsyncLocal state)
        // Direct execution ensures the side effect happens before Drain()
        DomainEventCollector.Clear();

        var drained = DomainEventCollector.Drain();
        await Assert.That(drained).IsEmpty();
    }

    [Test]
    public async Task AsyncLocal_MaintainsContextIsolation_BetweenConcurrentTasks()
    {
        DomainEventCollector.Add(TestDomainEventFactory.Create("main"));

        // AsyncLocal values flow TO child tasks (inherited at creation time).
        // The child sees "main" and drains it (setting its own context to null).
        // The parent's context is ISOLATED — it still has "main" after the child drains.
        var drained = await Task.Run(() =>
        {
            return DomainEventCollector.Drain();
        });

        var mainContext = DomainEventCollector.Drain();

        await Assert.That(drained.Count()).IsEqualTo(1);
        await Assert.That(((TestDomainEvent)drained[0]).Data).IsEqualTo("main");
        await Assert.That(mainContext.Count()).IsEqualTo(1);
        await Assert.That(((TestDomainEvent)mainContext[0]).Data).IsEqualTo("main");
    }

    [Test]
    public async Task EventType_ReturnsActualDerivedTypeName()
    {
        var evt = TestDomainEventFactory.Create();

        await Assert.That(evt.EventType).IsEqualTo(nameof(TestDomainEvent));
    }
}
