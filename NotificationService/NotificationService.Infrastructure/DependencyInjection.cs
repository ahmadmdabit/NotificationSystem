using System.Data;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Domain.Abstractions;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Repositories;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Dapper
        services.AddScoped<IDbConnection>(sp =>
            new Microsoft.Data.SqlClient.SqlConnection(
                configuration["AppSettings:SqlConnectionString"]));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationHistoryRepository, NotificationHistoryRepository>();

        // Messaging
        services.AddScoped<IDomainEventDispatcher, NotificationSentEventPublisher>();

        return services;
    }
}
