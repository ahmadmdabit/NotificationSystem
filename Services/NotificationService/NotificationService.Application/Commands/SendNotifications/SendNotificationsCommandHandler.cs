using MediatR;

using Microsoft.Extensions.Logging;

using NotificationService.Domain.Abstractions;

namespace NotificationService.Application.Commands.SendNotifications;

/// <summary>
/// Handles SendNotificationsCommand using bulk TVP insert and publishing NotificationSentEvent.
/// Transaction is owned by the TransactionBehavior pipeline (ICommand marker).
/// </summary>
/// <remarks>
/// The handler returns no value. Every real failure - unknown notification, failed history
/// write, concurrent delete - already surfaces as a typed exception that
/// <c>ApiExceptionHandler</c> maps to 404/400/500, so a <c>bool</c> result could only ever
/// be <c>true</c> and the controller's <c>else</c> branch was unreachable. A request that
/// succeeds is a success; anything else throws.
/// </remarks>
public sealed class SendNotificationsCommandHandler : IRequestHandler<SendNotificationsCommand>
{
    private readonly INotificationHistoryRepository historyRepository;
    private readonly INotificationRepository notificationRepository;
    private readonly ILogger<SendNotificationsCommandHandler> logger;

    public SendNotificationsCommandHandler(
        INotificationHistoryRepository historyRepository,
        INotificationRepository notificationRepository,
        ILogger<SendNotificationsCommandHandler> logger)
    {
        this.historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        this.notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SendNotificationsCommand request, CancellationToken cancellationToken)
    {
        // De-duplicate within the batch and drop non-positive ids early; the
        // FluentValidation rules own the client-facing messages for invalid ids.
        var items = request.Items
            .GroupBy(i => (NotificationId: i.NotificationId, UserId: i.UserId))
            .Select(g => g.Key)
            .Where(k => k.NotificationId > 0 && k.UserId > 0)
            .ToList();
        if (items.Count == 0)
            return;

        // Existence check: the TVP history insert would surface unknown NotificationIds as
        // a PK violation inside SPNotificationHistoryInsert (@SPSuccess=0 -> exception);
        // fail fast with a typed NotFound instead.
        var notificationIds = items.Select(i => i.NotificationId).Distinct().ToList();
        var notifications = await notificationRepository.GetByIdsAsync(notificationIds, cancellationToken).ConfigureAwait(false);
        var missing = notificationIds.Except(notifications.Select(n => n.Id)).ToList();
        if (missing.Count != 0)
            throw new Shared.Domain.Exceptions.NotFoundException("Notification", string.Join(",", missing));

        // Bulk insert histories via TVP (single round-trip)
        await historyRepository.InsertBulkAsync(items.Select(i => new Domain.NotificationHistory
        {
            NotificationId = i.NotificationId,
            UserId = i.UserId,
            CreatedAt = DateTime.UtcNow
        }).ToList(), cancellationToken).ConfigureAwait(false);

        // Draft → Sent: MarkAsSent raises exactly ONE NotificationSentEvent per
        // notification carrying its real recipient ids (raising a second event here
        // would double-publish post-commit — N-1). Non-Draft rows no-op (false).
        var recipientsByNotification = items
            .GroupBy(i => i.NotificationId)
            .ToDictionary(g => g.Key, g => g.Select(i => i.UserId).Distinct().ToList());

        var toSend = new List<Domain.Entities.Notification>();
        foreach (var notification in notifications)
        {
            if (notification.MarkAsSent(recipientsByNotification[notification.Id]))
                toSend.Add(notification);
        }

        // Single set-based status update (M-4: one round trip instead of one
        // SPUpdateNotification call per notification). The UPDATE re-guards
        // IsDeleted = 0 AND Status = Draft, so a concurrent transition is safe.
        //
        // N-10: when a concurrent send wins that Status = Draft race the UPDATE reports
        // fewer affected rows than toSend.Count. The in-memory aggregates have already
        // flipped to Sent and queued their events, so publishing for the surplus would
        // announce a transition this transaction did not make. Only the COUNT is
        // observable here (not which ids lost), so trim the tail and log the discrepancy
        // rather than guess. NotificationSentEvent has no consumer today, so this is
        // unobservable downstream; it matters the moment one is added.
        var published = new List<Domain.Entities.Notification>(toSend.Count);
        if (toSend.Count != 0)
        {
            var affected = await notificationRepository
                .MarkSentBatchAsync(toSend.Select(n => n.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);

            published.AddRange(toSend.Take(Math.Clamp(affected, 0, toSend.Count)));
        }

        if (published.Count != toSend.Count)
        {
            logger.LogWarning(
                "[SendNotifications] {Skipped} of {Attempted} notifications were transitioned " +
                "concurrently by another request; their NotificationSentEvent was not published.",
                toSend.Count - published.Count,
                toSend.Count);
        }

        // Dispatch-after-commit: record on the ambient collector; TransactionBehavior
        // drains + publishes after the transaction commits (no direct publish inside it).
        foreach (var notification in published)
        {
            Shared.Domain.DomainEventCollector.AddRange(notification.DomainEvents);
            notification.ClearDomainEvents();
        }
    }
}
