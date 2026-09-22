using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Events;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Application.Commands.SendNotifications;

/// <summary>
/// Handles SendNotificationsCommand using bulk TVP insert and publishing NotificationSentEvent.
/// Transaction is owned by the TransactionBehavior pipeline (ICommand marker).
/// </summary>
public sealed class SendNotificationsCommandHandler : IRequestHandler<SendNotificationsCommand, bool>
{
    private readonly INotificationHistoryRepository _historyRepository;
    private readonly INotificationRepository _notificationRepository;

    public SendNotificationsCommandHandler(
        INotificationHistoryRepository historyRepository,
        INotificationRepository notificationRepository)
    {
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
    }

    public async Task<bool> Handle(SendNotificationsCommand request, CancellationToken cancellationToken)
    {
        // De-duplicate within the batch and drop non-positive ids early; the
        // FluentValidation rules own the client-facing messages for invalid ids.
        var items = request.Items
            .GroupBy(i => (NotificationId: i.NotificationId, UserId: i.UserId))
            .Select(g => g.Key)
            .Where(k => k.NotificationId > 0 && k.UserId > 0)
            .ToList();
        if (items.Count == 0)
            return true;

        // Existence check: the TVP history insert would surface unknown NotificationIds as
        // a PK violation inside SPNotificationHistoryInsert (@SPSuccess=0 -> exception);
        // fail fast with a typed NotFound instead.
        var notificationIds = items.Select(i => i.NotificationId).Distinct().ToList();
        var notifications = await _notificationRepository.GetByIdsAsync(notificationIds, cancellationToken).ConfigureAwait(false);
        var missing = notificationIds.Except(notifications.Select(n => n.Id)).ToList();
        if (missing.Count != 0)
            throw new Shared.Domain.Exceptions.NotFoundException("Notification", string.Join(",", missing));

        // Bulk insert histories via TVP (single round-trip)
        await _historyRepository.InsertBulkAsync(items.Select(i => new Domain.NotificationHistory
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
        // sp_UpdateNotification call per notification). The UPDATE re-guards
        // IsDeleted = 0 AND Status = Draft, so a concurrent transition is safe.
        if (toSend.Count != 0)
        {
            await _notificationRepository
                .MarkSentBatchAsync(toSend.Select(n => n.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);
        }

        // Dispatch-after-commit: record on the ambient collector; TransactionBehavior
        // drains + publishes after the transaction commits (no direct publish inside it).
        foreach (var notification in toSend)
        {
            Shared.Domain.DomainEventCollector.AddRange(notification.DomainEvents);
            notification.ClearDomainEvents();
        }

        return true;
    }
}
