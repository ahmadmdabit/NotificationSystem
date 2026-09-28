using System.Net.Sockets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.Api;

/// <summary>
/// Reports broker reachability when the RabbitMQ transport is enabled (<c>Messaging:UseRabbitMq=true</c>),
/// so a container reported "healthy" does not hide a dead messaging path.
/// <para>
/// Scope: TCP connectivity to the configured broker endpoint. It deliberately does not verify
/// credentials or permissions (a wrong password still surfaces on the first publish).
/// </para>
/// </summary>
public sealed class MessagingHealthCheck : IHealthCheck
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);
    private const int DefaultRabbitMqPort = 5672;

    private readonly IConfiguration configuration;

    public MessagingHealthCheck(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Mirrors <c>MassTransitConfigurator.IsMessagingEnabled</c>. Duplicated rather than shared
    /// because <c>Shared.Api</c> must not reference a service Infrastructure project, and the key
    /// name is a string literal either way. The default is ON: an absent key must not silently
    /// report messaging as disabled.
    /// </summary>
    private static bool IsMessagingEnabled(IConfiguration configuration)
    {
        const string key = "Messaging:Enabled";
        return !bool.TryParse(configuration[key], out var enabled) || enabled;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Messaging:Enabled=false means no bus is registered at all and events are dropped by
        // NullDomainEventDispatcher. Reporting "healthy" for a RabbitMQ probe here would hide a
        // delivery path that does not exist, so say plainly that messaging is off.
        if (!IsMessagingEnabled(configuration))
        {
            return HealthCheckResult.Healthy(
                "Messaging is disabled (Messaging:Enabled=false); domain events are dropped, not published.");
        }

        var useRabbitMq = bool.TryParse(configuration["Messaging:UseRabbitMq"], out var parsed) && parsed;
        if (!useRabbitMq)
        {
            return HealthCheckResult.Healthy("In-memory transport in use; no broker required.");
        }

        var host = configuration["Messaging:RabbitMq:Host"] ?? "localhost";
        var port = int.TryParse(configuration["Messaging:RabbitMq:Port"], out var configuredPort) && configuredPort > 0
            ? configuredPort
            : DefaultRabbitMqPort;

        try
        {
            using var client = new TcpClient();
            using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probe.CancelAfter(ProbeTimeout);

            await client.ConnectAsync(host, port, probe.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"RabbitMQ reachable at {host}:{port}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"RabbitMQ unreachable at {host}:{port}.", ex);
        }
    }
}
