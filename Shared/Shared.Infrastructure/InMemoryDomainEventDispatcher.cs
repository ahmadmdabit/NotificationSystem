using Shared.Domain;

namespace Shared.Infrastructure;

/// <summary>
/// In-memory domain event dispatcher for local development and testing.
/// Stores dispatched events in an in-memory collection for inspection.
/// </summary>
public sealed class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly List<DomainEvent> _dispatchedEvents = new();

    public IReadOnlyList<DomainEvent> DispatchedEvents => _dispatchedEvents.AsReadOnly();

    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        _dispatchedEvents.Add(domainEvent);
        return Task.CompletedTask;
    }
}
