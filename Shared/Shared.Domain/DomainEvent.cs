namespace Shared.Domain;

/// <summary>
/// Base class for all domain events.
/// </summary>
public abstract class DomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}

/// <summary>
/// Ambient, async-flow collector for aggregate domain events raised inside a MediatR
/// command handler. <see cref="Application"/>-level <c>TransactionBehavior</c> drains it
/// only after the transaction commits (dispatch-after-commit), so broker publishes can
/// never precede the DB write. Uses <see cref="AsyncLocal{T}"/> so concurrent requests
/// on different async flows stay isolated. Handlers record events here in addition to
/// (or instead of) holding aggregate references.
/// </summary>
public static class DomainEventCollector
{
    private static readonly AsyncLocal<List<DomainEvent>?> pending = new();

    /// <summary>Records an event for post-commit dispatch.</summary>
    public static void Add(DomainEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        (pending.Value ??= []).Add(evt);
    }

    /// <summary>Records a batch of events for post-commit dispatch.</summary>
    public static void AddRange(IEnumerable<DomainEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var evt in events)
            Add(evt);
    }

    /// <summary>Removes and returns all pending events, leaving the collector empty.</summary>
    public static List<DomainEvent> Drain()
    {
        var pending = DomainEventCollector.pending.Value;
        DomainEventCollector.pending.Value = null;
        return pending ?? [];
    }

    /// <summary>
    /// Discards all pending events (called on transaction rollback).
    /// </summary>
    public static void Clear() => pending.Value = null;

    /// <summary>
    /// Creates the pending list in the <b>calling</b> execution context so that events
    /// added by an awaited callee are observable here.
    /// <para>
    /// <see cref="AsyncLocal{T}"/> flows <i>into</i> an awaited callee, but mutations made
    /// inside that callee do <b>not</b> flow back. When <c>pending.Value</c> is null and a
    /// handler calls <see cref="Add"/>, the <c>??= []</c> allocates a list that only the
    /// handler's context can see — so a post-commit <see cref="Drain"/> in the pipeline
    /// returns empty and the event is silently never dispatched.
    /// </para>
    /// <para>
    /// Seeding the list first makes the handler's <c>Add</c> mutate this shared instance,
    /// so the reference is visible to the caller after the await completes.
    /// </para>
    /// </summary>
    public static void Seed() => pending.Value ??= [];
}

