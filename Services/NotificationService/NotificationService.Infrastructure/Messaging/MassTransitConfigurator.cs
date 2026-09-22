using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit with RabbitMQ transport. In-memory transport for local dev.
/// </summary>
public static class MassTransitConfigurator
{
    public static IServiceCollection AddMassTransitForNotificationService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRabbitMq = bool.TryParse(configuration["Messaging:UseRabbitMq"], out var parsed) && parsed;

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
