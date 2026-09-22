using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Domain.Abstractions;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Repositories;
using Shared.Application.Abstractions;
using Shared.Domain.Abstractions;
using Shared.Infrastructure;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Dapper global configuration
        DapperConfiguration.Configure();

        // Configuration
        services.Configure<Shared.Helpers.AppSettings>(configuration.GetSection("AppSettings"));

        // Dapper
        services.AddScoped<IDbConnection>(sp =>
            new Microsoft.Data.SqlClient.SqlConnection(
                configuration["AppSettings:SqlConnectionString"]));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationHistoryRepository, NotificationHistoryRepository>();

        // Messaging (in-memory for dev, RabbitMQ for prod)
        services.AddMassTransitForNotificationService(configuration);

        // Domain event dispatcher — single shared MassTransit implementation
        // (DRY: replaces the identical per-service *EventPublisher clones).
        services.AddScoped<IDomainEventDispatcher, MassTransitDomainEventDispatcher>();

        return services;
    }
}