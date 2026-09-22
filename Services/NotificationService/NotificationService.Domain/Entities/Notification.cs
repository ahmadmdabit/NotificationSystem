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

    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

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

    public void AddDomainEvent(DomainEvent evt) => _domainEvents.Add(evt);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
