using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Abstractions;
using Shared.Domain.Abstractions;
using Shared.Helpers;
using Shared.Infrastructure;
using UserService.Domain.Abstractions;
using UserService.Infrastructure.Messaging;
using UserService.Infrastructure.Repositories;
using UserService.Infrastructure.Services;

namespace UserService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUserServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Dapper global configuration
        DapperConfiguration.Configure();

        // Configuration
        services.Configure<AppSettings>(configuration.GetSection("AppSettings"));

        // Dapper
        services.AddScoped<IDbConnection>(sp =>
            new Microsoft.Data.SqlClient.SqlConnection(
                configuration["AppSettings:SqlConnectionString"]));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repository
        services.AddScoped<IUserRepository, UserRepository>();

        // Domain services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Messaging (in-memory for dev, RabbitMQ for prod)
        services.AddMassTransitForUserService(configuration);

        // Domain event dispatcher — single shared MassTransit implementation
        // (DRY: replaces the identical per-service *EventPublisher clones).
        services.AddScoped<IDomainEventDispatcher, MassTransitDomainEventDispatcher>();

        return services;
    }
}