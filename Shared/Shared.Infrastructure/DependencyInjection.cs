using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers shared infrastructure services. MassTransit RabbitMQ is configured
    /// in each service's Infrastructure project (not here) so Shared stays lightweight.
    /// </summary>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        // Default dispatcher: in-memory for local dev. Override in service Startup.cs
        // when RabbitMQ transport is configured.
        services.AddSingleton<IDomainEventDispatcher, InMemoryDomainEventDispatcher>();
        return services;
    }
}
