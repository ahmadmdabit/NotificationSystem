using Shared.Domain;

namespace UserService.Domain.Abstractions;

/// <summary>
/// Domain event dispatcher for UserService.
/// </summary>
public interface IDomainEventDispatcher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : DomainEvent;
}
