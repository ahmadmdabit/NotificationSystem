using NotificationService.Application.Commands.SendNotifications;
using NotificationService.Domain;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Events;
using NotificationService.Domain.ValueObjects;

using Shared.Domain;
using Shared.Domain.Exceptions;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class SendNotificationsCommandHandlerTests
{
    // DomainEventCollector is static ambient state (AsyncLocal). TUnit builds a fresh
    // class instance per test but does NOT reset statics, and tests run in parallel —
    // without this the events would leak between tests and across classes.
    [After(HookType.Test)]
    public void TearDown() => DomainEventCollector.Clear();

    [Test]
    public async Task Constructor_WhenHistoryRepositoryNull_ThrowsArgumentNullException()
    {
        var notificationRepository = MockNotificationRepository.Create().Object;

        await Assert.That(() => new SendNotificationsCommandHandler(null!, notificationRepository, Mock.Logger<SendNotificationsCommandHandler>()))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("historyRepository");
    }

    [Test]
    public async Task Constructor_WhenLoggerNull_ThrowsArgumentNullException()
    {
        // A DI-registered handler must take interfaces, never a primitive or an optional
        // dependency. A null-tolerant logger would let a mis-registration surface as a
        // NullReferenceException inside the post-commit path instead of at resolve time.
        var historyRepository = MockNotificationHistoryRepository.Create().Object;
        var notificationRepository = MockNotificationRepository.Create().Object;

        await Assert.That(() => new SendNotificationsCommandHandler(historyRepository, notificationRepository, null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public async Task Constructor_WhenNotificationRepositoryNull_ThrowsArgumentNullException()
    {
        var historyRepository = MockNotificationHistoryRepository.Create().Object;

        await Assert.That(() => new SendNotificationsCommandHandler(historyRepository, null!, Mock.Logger<SendNotificationsCommandHandler>()))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("notificationRepository");
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task Handle_WhenAllIdsAreNonPositive_ReturnsTrueWithoutPersistence(long invalidId)
    {
        // Arrange
        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        var handler = new SendNotificationsCommandHandler(historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        // Act — non-positive ids are filtered out before any I/O
        await handler.Handle(
            new SendNotificationsCommand
            {
                Items = [new SendNotificationItem { NotificationId = invalidId, UserId = invalidId }]
            },
            CancellationToken.None);

        // Assert
        notificationRepository!.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>()).WasNeverCalled();
        historyRepository!.InsertBulkAsync(Any<IReadOnlyList<NotificationHistory>>(), Any<CancellationToken>()).WasNeverCalled();
        await Assert.That(DomainEventCollector.Drain()).IsEmpty();
    }

    [Test]
    public async Task Handle_WhenNotificationDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange — repository returns nothing, so the id is unknown
        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        var handler = new SendNotificationsCommandHandler(historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        // Act / Assert — fail fast rather than letting the TVP insert hit a PK violation
        var exception = await Assert.That(async () => await handler.Handle(
                new SendNotificationsCommand
                {
                    Items = [new SendNotificationItem { NotificationId = 7, UserId = 1 }]
                },
                CancellationToken.None))
            .ThrowsExactly<NotFoundException>();

        await Assert.That(exception!.Message).Contains("7");

        // Nothing may be written when the batch is rejected
        historyRepository!.InsertBulkAsync(Any<IReadOnlyList<NotificationHistory>>(), Any<CancellationToken>()).WasNeverCalled();
    }

    [Test]
    public async Task Handle_WhenValidBatch_DeduplicatesInsertsHistoriesFlipsStatusAndCollectsEvents()
    {
        // Arrange — the same (id, user) pair twice must collapse to a single history row
        var notification = Notification.Rehydrate(5, "Title", "Content", NotificationStatus.Draft);
        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        notificationRepository!.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>())
            .Returns([notification]);
        var handler = new SendNotificationsCommandHandler(historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        // Seed the collector exactly as TransactionBehavior now does in production.
        // Without this, AsyncLocal semantics mean the handler's Add() allocates a list
        // this test cannot see. The pipeline-level guarantee is covered separately by
        // Shared.Tests TransactionBehaviorTests.Handle_HandlerAddsEventInsideNext_PublishesItPostCommit;
        // this test only asserts the handler's own contract — that it records the event.
        DomainEventCollector.Clear();
        DomainEventCollector.Seed();

        // Act
        await handler.Handle(
            new SendNotificationsCommand
            {
                Items =
                [
                    new SendNotificationItem { NotificationId = 5, UserId = 1 },
                    new SendNotificationItem { NotificationId = 5, UserId = 1 }
                ]
            },
            CancellationToken.None);

        // Assert
        await Assert.That(notification.Status).IsEqualTo(NotificationStatus.Sent);

        // Distinct ids only — one lookup, not one per item
        notificationRepository.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>()).WasCalled(Times.Once);

        // Exactly one bulk insert, whatever the input count
        historyRepository!.InsertBulkAsync(Any<IReadOnlyList<NotificationHistory>>(), Any<CancellationToken>())
            .WasCalled(Times.Once);

        // A single set-based status flip for the batch (M-4)
        notificationRepository.MarkSentBatchAsync(Any<IReadOnlyCollection<long>>(), Any<CancellationToken>())
            .WasCalled(Times.Once);

        // Exactly ONE event on the collector (N-1) carrying the real recipient ids
        var events = DomainEventCollector.Drain();
        await Assert.That(events).Count().IsEqualTo(1);
        var sent = events[0] as NotificationSentEvent;
        await Assert.That(sent).IsNotNull();
        await Assert.That(sent!.RecipientIds).IsNotNull();

        // Dispatch-after-commit: the aggregate must be drained, not left holding the event
        await Assert.That(notification.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task Handle_WhenNotificationAlreadySent_DoesNotReEmitDomainEvent()
    {
        // Arrange — transition it first, so MarkAsSent is a no-op on this pass
        var notification = Notification.Rehydrate(5, "Title", "Content", NotificationStatus.Draft);
        notification.MarkAsSent([1L]);
        notification.ClearDomainEvents();
        DomainEventCollector.Clear();

        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        notificationRepository!.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>())
            .Returns([notification]);
        var handler = new SendNotificationsCommandHandler(historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        // Act — history is still recorded, but the status flip and event emission are skipped
        await handler.Handle(
            new SendNotificationsCommand
            {
                Items = [new SendNotificationItem { NotificationId = 5, UserId = 1 }]
            },
            CancellationToken.None);

        // Assert
        historyRepository!.InsertBulkAsync(Any<IReadOnlyList<NotificationHistory>>(), Any<CancellationToken>())
            .WasCalled(Times.Once);

        // toSend is empty, so neither the batch UPDATE nor the event may happen
        notificationRepository.MarkSentBatchAsync(Any<IReadOnlyCollection<long>>(), Any<CancellationToken>())
            .WasNeverCalled();
        await Assert.That(DomainEventCollector.Drain()).IsEmpty();
    }

    [Test]
    public async Task Handle_WhenConcurrentRequestWinsTheDraftRace_DoesNotPublishEventsForUnflippedRows()
    {
        // N-10. MarkSentBatchAsync's UPDATE re-guards Status = Draft, so a concurrent send
        // that already flipped a row makes this request's UPDATE affect fewer rows than it
        // asked for. The in-memory aggregate has already flipped and queued its event, so
        // publishing for the surplus would announce a transition this transaction did not
        // make. Assert on the collector, not on a return value -- there is no return value.
        var first = Notification.Rehydrate(5, "T1", "C1", NotificationStatus.Draft);
        var second = Notification.Rehydrate(6, "T2", "C2", NotificationStatus.Draft);

        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        notificationRepository!.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>())
            .Returns([first, second]);

        // Only ONE of the two rows was actually transitioned by this request.
        notificationRepository
            .MarkSentBatchAsync(Any<IReadOnlyCollection<long>>(), Any<CancellationToken>())
            .Returns(1);

        var handler = new SendNotificationsCommandHandler(
            historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        DomainEventCollector.Clear();
        DomainEventCollector.Seed();

        await handler.Handle(
            new SendNotificationsCommand
            {
                Items =
                [
                    new SendNotificationItem { NotificationId = 5, UserId = 1 },
                    new SendNotificationItem { NotificationId = 6, UserId = 1 }
                ]
            },
            CancellationToken.None);

        // Exactly one event, for the one row the UPDATE actually flipped.
        var events = DomainEventCollector.Drain();
        await Assert.That(events).Count().IsEqualTo(1);
        await Assert.That((events[0] as NotificationSentEvent)!.NotificationId).IsEqualTo(5L);
    }

    [Test]
    public async Task Handle_WhenBatchUpdateAffectsEveryRow_PublishesOneEventPerNotification()
    {
        // The N-10 guard must not over-trim: when the UPDATE reports every row, every event
        // is still published. Without this, "take(affected)" could silently drop real events.
        var first = Notification.Rehydrate(5, "T1", "C1", NotificationStatus.Draft);
        var second = Notification.Rehydrate(6, "T2", "C2", NotificationStatus.Draft);

        var historyRepository = MockNotificationHistoryRepository.Create();
        var notificationRepository = MockNotificationRepository.Create();
        notificationRepository!.GetByIdsAsync(Any<IEnumerable<long>>(), Any<CancellationToken>())
            .Returns([first, second]);
        notificationRepository
            .MarkSentBatchAsync(Any<IReadOnlyCollection<long>>(), Any<CancellationToken>())
            .Returns(2);

        var handler = new SendNotificationsCommandHandler(
            historyRepository.Object, notificationRepository.Object, Mock.Logger<SendNotificationsCommandHandler>());

        DomainEventCollector.Clear();
        DomainEventCollector.Seed();

        await handler.Handle(
            new SendNotificationsCommand
            {
                Items =
                [
                    new SendNotificationItem { NotificationId = 5, UserId = 1 },
                    new SendNotificationItem { NotificationId = 6, UserId = 1 }
                ]
            },
            CancellationToken.None);

        var events = DomainEventCollector.Drain();
        await Assert.That(events).Count().IsEqualTo(2);
    }
}