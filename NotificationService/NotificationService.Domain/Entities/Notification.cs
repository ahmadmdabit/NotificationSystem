using NotificationService.Domain.ValueObjects;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Rich Notification entity with domain behavior.
/// </summary>
public sealed class Notification
{
    public long Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        return new Notification
        {
            Title = title,
            Content = content,
            Status = NotificationStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsSent()
    {
        if (Status == NotificationStatus.Sent)
            throw new InvalidOperationException("Notification is already sent.");

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string reason)
    {
        if (Status == NotificationStatus.Sent)
            throw new InvalidOperationException("Cannot mark a sent notification as failed.");

        Status = NotificationStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }
}
