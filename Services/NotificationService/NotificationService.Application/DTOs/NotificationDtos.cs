namespace NotificationService.Application.DTOs;

/// <summary>
/// Notification data transfer object.
/// </summary>
public sealed class NotificationDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
