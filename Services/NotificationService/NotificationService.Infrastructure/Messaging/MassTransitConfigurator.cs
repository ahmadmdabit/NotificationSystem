using MassTransit;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit with RabbitMQ transport. In-memory transport for local dev.
/// </summary>
public static class MassTransitConfigurator
{
    /// <summary>
    /// Config key that switches the bus on. When false, <b>no MassTransit registration is made
    /// at all</b> and the caller must register a null-object dispatcher instead.
    /// </summary>
    public const string EnabledKey = "Messaging:Enabled";

    public static bool IsMessagingEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Default ON: an absent key must not silently disable the broker in a deployment.
        return !bool.TryParse(configuration[EnabledKey], out var enabled) || enabled;
    }

    public static IServiceCollection AddMassTransitForNotificationService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRabbitMq = bool.TryParse(configuration["Messaging:UseRabbitMq"], out var parsed) && parsed;

        // MassTransit 9.x requires a commercial licence and enforces the gate at bus creation,
        // BEFORE transport selection — so the in-memory path is not a licence-free path and
        // flipping Messaging__UseRabbitMq=false does not avoid the failure. Callers that need to
        // run without a licence check IsMessagingEnabled() first and skip this method entirely.
        services.AddMassTransit(cfg =>
        {
            if (useRabbitMq)
            {
                cfg.UsingRabbitMq((context, busCfg) =>
                {
                    busCfg.Host(configuration["Messaging:RabbitMq:Host"] ?? "localhost", h =>
                    {
                        h.Username(configuration["Messaging:RabbitMq:Username"] ?? "guest");
                        h.Password(configuration["Messaging:RabbitMq:Password"] ?? "guest");
                    });

                    busCfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                cfg.UsingInMemory((context, busCfg) =>
                {
                    busCfg.ConfigureEndpoints(context);
                });
            }
        });

        return services;
    }
}
