using MassTransit;
using Shared.Domain;

namespace Shared.Infrastructure;

/// <summary>
/// Production domain event dispatcher using MassTransit (in-memory or RabbitMQ transport,
/// configured by each service's MassTransitConfigurator).
/// </summary>
public sealed class MassTransitDomainEventDispatcher : Shared.Domain.Abstractions.IDomainEventDispatcher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitDomainEventDispatcher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : Shared.Domain.DomainEvent
    {
        await _publishEndpoint.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
