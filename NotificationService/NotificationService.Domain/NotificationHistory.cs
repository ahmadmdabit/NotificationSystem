namespace NotificationService.Domain;

/// <summary>
/// Infrastructure-level DTO for the NotificationHistories join table.
/// NOT a domain entity (no domain behavior) and NOT a value object (has composite identity).
/// </summary>
public sealed class NotificationHistory
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
