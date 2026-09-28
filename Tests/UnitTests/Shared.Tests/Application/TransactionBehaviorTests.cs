using MediatR;

using Microsoft.Extensions.Logging.Abstractions;

using Shared.Application.Behaviors;
using Shared.Domain;

using TestDoubles.Helpers;
using TestDoubles.Mocks;
using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Application;

public class TransactionBehaviorTests
{
    [Before(HookType.Test)]
    public void SetUp()
    {
        DomainEventCollector.Clear();
    }

    [Test]
    public async Task Handle_NonCommandRequest_BypassesTransactionAndDispatcher()
    {
        var uow = MockUnitOfWork.Create();
        var (dispatcher, _) = MockDomainEventDispatcher.Create();
        var behavior = new TransactionBehavior<QueryWithoutResult, int>(
            uow.Object, dispatcher.Object, NullLogger<TransactionBehavior<QueryWithoutResult, int>>.Instance);

        var result = await behavior.Handle(new QueryWithoutResult(), TransactionTestFixtures.Return42, CancellationToken.None);

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(uow.BeginTransactionAsync(Any<CancellationToken>())).WasNeverCalled();
        await Assert.That(uow.CommitAsync(Any<CancellationToken>())).WasNeverCalled();
    }

    [Test]
    public async Task Handle_CommandSuccess_BeginsCommitsAndDispatchesEventsPostCommit()
    {
        var uow = MockUnitOfWork.Create();
        var (dispatcher, publishedEvents) = MockDomainEventDispatcher.Create();
        var behavior = new TransactionBehavior<CommandWithResult, int>(
            uow.Object, dispatcher.Object, NullLogger<TransactionBehavior<CommandWithResult, int>>.Instance);

        var evt = TestDomainEventFactory.Create();
        DomainEventCollector.Add(evt);

        var result = await behavior.Handle(new CommandWithResult(), TransactionTestFixtures.Return99, CancellationToken.None);

        await Assert.That(result).IsEqualTo(99);
        await Assert.That(uow.BeginTransactionAsync(Any<CancellationToken>())).WasCalled(Times.Once);
        await Assert.That(uow.CommitAsync(Any<CancellationToken>())).WasCalled(Times.Once);
        await Assert.That(dispatcher.PublishAsync(Any<DomainEvent>(), Any<CancellationToken>())).WasCalled(Times.Once);
        await Assert.That(publishedEvents.Values.Count()).IsEqualTo(1);
        await Assert.That(publishedEvents.Values.First()).IsEqualTo(evt);
    }

    [Test]
    public async Task Handle_HandlerAddsEventInsideNext_PublishesItPostCommit()
    {
        // Regression guard (2026-09): AsyncLocal mutations do NOT flow back out of an
        // awaited callee. The production path records events from INSIDE the handler, so
        // the pipeline must seed the collector in its own context — otherwise the
        // handler's Add() allocates a list the pipeline cannot see and the event is
        // silently dropped. The tests above add events BEFORE calling Handle, which does
        // not exercise this path and passed while the bug was still live.
        var uow = MockUnitOfWork.Create();
        var (dispatcher, publishedEvents) = MockDomainEventDispatcher.Create();
        var behavior = new TransactionBehavior<CommandWithResult, int>(
            uow.Object, dispatcher.Object, NullLogger<TransactionBehavior<CommandWithResult, int>>.Instance);

        DomainEventCollector.Clear();
        var evt = TestDomainEventFactory.Create("from-handler");

        // The handler records the event, exactly as a real command handler does
        RequestHandlerDelegate<int> next = async _ =>
        {
            await Task.Yield();
            DomainEventCollector.Add(evt);
            return 99;
        };

        var result = await behavior.Handle(new CommandWithResult(), next, CancellationToken.None);

        await Assert.That(result).IsEqualTo(99);
        await Assert.That(uow.CommitAsync(Any<CancellationToken>())).WasCalled(Times.Once);
        await Assert.That(dispatcher.PublishAsync(Arg.Is<DomainEvent>(e => ReferenceEquals(e, evt)), Any<CancellationToken>()))
            .WasCalled(Times.Once);
        await Assert.That(publishedEvents.Values.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Handle_CommandThrowsException_RollsBackTransactionAndClearsDomainEvents()
    {
        var uow = MockUnitOfWork.Create();
        var (dispatcher, _) = MockDomainEventDispatcher.Create();
        var behavior = new TransactionBehavior<CommandWithResult, int>(
            uow.Object, dispatcher.Object, NullLogger<TransactionBehavior<CommandWithResult, int>>.Instance);

        var evt = TestDomainEventFactory.Create();
        DomainEventCollector.Add(evt);

        RequestHandlerDelegate<int> next = _ => throw new InvalidOperationException("handler failed");

        var ex = await Assert.That(() =>
            behavior.Handle(new CommandWithResult(), next, CancellationToken.None))
            .Throws<InvalidOperationException>();

        await Assert.That(ex!.Message).IsEqualTo("handler failed");
        await Assert.That(uow.RollbackAsync(Any<CancellationToken>())).WasCalled(Times.Once);
        await Assert.That(uow.CommitAsync(Any<CancellationToken>())).WasNeverCalled();
        await Assert.That(dispatcher.PublishAsync(Any<DomainEvent>(), Any<CancellationToken>())).WasNeverCalled();
    }
}
