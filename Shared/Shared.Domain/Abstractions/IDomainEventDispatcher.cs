using Shared.Domain;

namespace Shared.Domain.Abstractions;

/// <summary>
/// Abstraction for publishing domain events. Single shared contract (DRY) —
/// implemented by Shared.Infrastructure dispatchers, aliased by service Domains.
/// </summary>
public interface IDomainEventDispatcher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : DomainEvent;
}
