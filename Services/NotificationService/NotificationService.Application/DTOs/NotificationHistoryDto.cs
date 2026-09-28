namespace NotificationService.Application.DTOs;

/// <summary>
/// Notification history data transfer object.
/// </summary>
public sealed class NotificationHistoryDto
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public DateTime? CreatedAt { get; set; }
}
