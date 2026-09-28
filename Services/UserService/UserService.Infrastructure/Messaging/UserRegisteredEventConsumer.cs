using MassTransit;

using Microsoft.Extensions.Logging;

using UserService.Domain.Events;

namespace UserService.Infrastructure.Messaging;

/// <summary>
/// Consumes UserRegisteredEvent from RabbitMQ and logs/processes it.
/// </summary>
public sealed class UserRegisteredEventConsumer : IConsumer<UserRegisteredEvent>
{
    /// <summary>Message template used for the structured log entry.</summary>
    internal const string LogMessageTemplate = "[UserRegisteredEvent] UserId={UserId} Username={Username}";

    private readonly ILogger<UserRegisteredEventConsumer> logger;

    public UserRegisteredEventConsumer(ILogger<UserRegisteredEventConsumer> logger)
    {
        this.logger = logger;
    }

    public Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Handle(context.Message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a user registration.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="Consume"/> so the behaviour is verifiable without a
    /// <see cref="ConsumeContext{T}"/>: the TUnit.Mocks source generator cannot generate a
    /// mock for that type, because its own type parameter <c>T</c> collides with the generated
    /// extension container's <c>T</c>.
    /// <para>
    /// <c>internal</c>, not <c>public</c>, with
    /// <c>[assembly: InternalsVisibleTo("UserService.Tests")]</c> in
    /// <c>Properties/AssemblyInfo.cs</c>. Extracting the method is what makes it testable;
    /// making it public also puts it on the assembly's public surface for every other consumer,
    /// which is not a price worth paying for a test seam (F-08).
    /// </para>
    /// </remarks>
    internal void Handle(UserRegisteredEvent notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        logger.LogInformation(LogMessageTemplate, notification.UserId, notification.Username);
    }
}
