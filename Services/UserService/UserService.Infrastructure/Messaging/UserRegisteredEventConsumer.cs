using MassTransit;
using Microsoft.Extensions.Logging;
using UserService.Domain.Events;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// Consumes UserRegisteredEvent from RabbitMQ and logs/processes it.
/// </summary>
public sealed class UserRegisteredEventConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly ILogger<UserRegisteredEventConsumer> _logger;

    public UserRegisteredEventConsumer(ILogger<UserRegisteredEventConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var evt = context.Message;
        _logger.LogInformation("[UserRegisteredEvent] UserId={UserId} Username={Username}", evt.UserId, evt.Username);
        return Task.CompletedTask;
    }
}
