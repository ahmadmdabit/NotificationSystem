namespace NotificationService.Domain.Abstractions;

/// <summary>
/// Domain event dispatcher for NotificationService.
/// </summary>
public interface IDomainEventDispatcher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : Shared.Domain.DomainEvent;
}
