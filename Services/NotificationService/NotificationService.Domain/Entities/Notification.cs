using NotificationService.Domain.Events;
using NotificationService.Domain.ValueObjects;

using Shared.Domain;

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

    private readonly List<DomainEvent> domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => domainEvents.AsReadOnly();

    private Notification() { }

    /// <summary>
    /// Rehydrates an existing row from persistence. Bypasses the Draft-state and
    /// validation rules of <see cref="Create"/> because the state already exists —
    /// there is nothing to validate, and the current status must be preserved.
    /// </summary>
    public static Notification Rehydrate(
        long id,
        string title,
        string content,
        NotificationStatus status,
        DateTime? sentAt = null,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        return new Notification
        {
            Id = id,
            Title = title,
            Content = content,
            Status = status,
            SentAt = sentAt,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

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

    public void Update(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        Title = title;
        Content = content;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Draft → Sent transition. Idempotent: non-Draft states (Sent/Failed/…) are a
    /// no-op and return <c>false</c>. Raises exactly ONE <see cref="NotificationSentEvent"/>
    /// carrying the real recipient ids — callers must not append their own copy
    /// (a second event would be double-published post-commit).
    /// </summary>
    public bool MarkAsSent(IReadOnlyList<long> recipientUserIds)
    {
        ArgumentNullException.ThrowIfNull(recipientUserIds);

        if (Status != NotificationStatus.Draft)
            return false;

        Status = NotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new NotificationSentEvent(Id, recipientUserIds.ToList()));
        return true;
    }

    public void AddDomainEvent(DomainEvent evt) => domainEvents.Add(evt);
    public void ClearDomainEvents() => domainEvents.Clear();
}
