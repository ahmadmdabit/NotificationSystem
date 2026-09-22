using Shared.Domain;

namespace NotificationService.Domain.Events;

/// <summary>
/// Domain event raised when notifications are sent.
/// </summary>
public sealed class NotificationSentEvent : DomainEvent
{
    public long NotificationId { get; }
    public IReadOnlyList<long> RecipientIds { get; }

    public NotificationSentEvent(long notificationId, IReadOnlyList<long> recipientIds)
    {
        NotificationId = notificationId;
        RecipientIds = recipientIds;
    }
}
