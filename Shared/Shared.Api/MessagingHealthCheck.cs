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

    private readonly IConfiguration _configuration;

    public MessagingHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var useRabbitMq = bool.TryParse(_configuration["Messaging:UseRabbitMq"], out var parsed) && parsed;
        if (!useRabbitMq)
        {
            return HealthCheckResult.Healthy("In-memory transport in use; no broker required.");
        }

        var host = _configuration["Messaging:RabbitMq:Host"] ?? "localhost";
        var port = int.TryParse(_configuration["Messaging:RabbitMq:Port"], out var configuredPort) && configuredPort > 0
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
