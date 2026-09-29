using MassTransit;

namespace Shared.Infrastructure;

/// <summary>
/// Production domain event dispatcher using MassTransit (in-memory or RabbitMQ transport,
/// configured by each service's MassTransitConfigurator).
/// </summary>
public sealed class MassTransitDomainEventDispatcher : Shared.Domain.Abstractions.IDomainEventDispatcher
{
    private readonly IPublishEndpoint publishEndpoint;

    public MassTransitDomainEventDispatcher(IPublishEndpoint publishEndpoint)
    {
        this.publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : Shared.Domain.DomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // Publish on the RUNTIME type, deliberately defeating generic inference.
        //
        // MassTransit derives the exchange name from the *static* type argument of
        // Publish<T>, not from message.GetType(). TransactionBehavior drains a
        // List<DomainEvent>, so T infers as the base type and every event was published to
        // the base-class exchange Shared.Domain:DomainEvent — which has no bindings, so
        // RabbitMQ accepted and discarded the message. The queue is bound only to the
        // concrete exchange (UserService.Domain.Events:UserRegisteredEvent), which therefore
        // never saw traffic: no exception, HTTP 200, empty queue, silent consumer.
        //
        // Casting to object selects the non-generic overload, so the exchange is derived from
        // GetType() and lands on the exchange the consumer actually binds to.
        await publishEndpoint.Publish((object)domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
