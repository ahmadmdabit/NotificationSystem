using Shared.Domain;

namespace Shared.Infrastructure;

/// <summary>
/// Abstraction for publishing domain events.
/// </summary>
public interface IDomainEventDispatcher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : DomainEvent;
}
