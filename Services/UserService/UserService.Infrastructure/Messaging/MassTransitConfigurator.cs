using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// Configures MassTransit with RabbitMQ transport. In-memory transport for local dev.
/// Both branches call ConfigureEndpoints so consumer topology stays identical
/// across transports (prevents dev/prod divergence when consumers are registered).
/// </summary>
public static class MassTransitConfigurator
{
    public static IServiceCollection AddMassTransitForUserService(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var useRabbitMq = bool.TryParse(configuration["Messaging:UseRabbitMq"], out var parsed) && parsed;

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumer<UserRegisteredEventConsumer>();

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
