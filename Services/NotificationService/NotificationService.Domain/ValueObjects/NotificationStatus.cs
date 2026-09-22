namespace NotificationService.Domain.ValueObjects;

/// <summary>
/// Value object representing notification status.
/// </summary>
public enum NotificationStatus
{
    Draft = 0,
    Sent = 1,
    Failed = 2
}
