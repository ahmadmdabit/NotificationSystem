using Microsoft.Extensions.Logging;

using Shared.Domain;
using Shared.Domain.Abstractions;

namespace Shared.Infrastructure;

/// <summary>
/// <see cref="IDomainEventDispatcher"/> used when messaging is switched off
/// (<c>Messaging:Enabled=false</c>). Discards every event and says so at Debug level.
/// </summary>
/// <remarks>
/// <para>
/// This exists because MassTransit 9.x refuses to create a bus without a commercial licence —
/// <i>including</i> the in-memory transport, because the licence gate runs at bus creation,
/// before transport selection. Without a null object, disabling the bus would leave
/// <see cref="IDomainEventDispatcher"/> unresolvable and take down the post-commit dispatch path
/// in <c>TransactionBehavior</c> entirely, which is a worse failure than losing events.
/// </para>
/// <para>
/// <b>Events are genuinely dropped here, not queued.</b> That is the intended trade for running
/// without a licence: local development and CI keep working, and no code path silently pretends
/// delivery succeeded. The Debug log is the only trace — do not rely on it in production, where
/// messaging must stay enabled.
/// </para>
/// </remarks>
public sealed class NullDomainEventDispatcher(ILogger<NullDomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        logger.LogDebug(
            "Messaging is disabled; dropping {DomainEventType} instead of publishing it.",
            domainEvent.GetType().Name);

        return Task.CompletedTask;
    }
}
