using FluentValidation;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace NotificationService.Application;

/// <summary>
/// Dependency injection registration for NotificationService.Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationServiceApplication(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Pipeline behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Shared.Application.Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Shared.Application.Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Shared.Application.Behaviors.TransactionBehavior<,>));

        return services;
    }
}
