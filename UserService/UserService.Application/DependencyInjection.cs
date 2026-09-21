using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Behaviors;

namespace UserService.Application;

/// <summary>
/// Dependency injection registration for UserService.Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddUserServiceApplication(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Pipeline behaviors
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
