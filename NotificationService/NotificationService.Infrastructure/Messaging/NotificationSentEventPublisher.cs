using MassTransit;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Events;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Publishes NotificationSentEvent via MassTransit IPublishEndpoint.
/// </summary>
public sealed class NotificationSentEventPublisher : IDomainEventDispatcher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public NotificationSentEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : Shared.Domain.DomainEvent
    {
        await _publishEndpoint.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
