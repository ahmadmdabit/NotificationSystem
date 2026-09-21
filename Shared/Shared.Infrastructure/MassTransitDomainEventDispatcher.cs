using MassTransit;
using Shared.Domain;

namespace Shared.Infrastructure;

/// <summary>
/// Production domain event dispatcher using MassTransit with RabbitMQ transport.
/// </summary>
public sealed class MassTransitDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitDomainEventDispatcher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        await _publishEndpoint.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
