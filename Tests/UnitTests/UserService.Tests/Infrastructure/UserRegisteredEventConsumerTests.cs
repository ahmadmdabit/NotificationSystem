using Microsoft.Extensions.Logging;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.Mocks.Logging;

using UserService.Domain.Events;
using UserService.Infrastructure.Messaging;

namespace UserService.Tests.Infrastructure;

public class UserRegisteredEventConsumerTests
{
    [Test]
    public async Task Handle_LogsUserRegisteredEventDetails()
    {
        // Arrange
        var logger = Mock.Logger<UserRegisteredEventConsumer>();
        var consumer = new UserRegisteredEventConsumer(logger);
        var notification = new UserRegisteredEvent(1, "TestUser");

        // Act
        consumer.Handle(notification);

        // Assert
        logger.VerifyLog()
            .AtLevel(LogLevel.Information)
            .ContainingMessage("TestUser")
            .WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_LogsUserIdAndUsernameAsStructuredValues()
    {
        // Arrange
        var logger = Mock.Logger<UserRegisteredEventConsumer>();
        var consumer = new UserRegisteredEventConsumer(logger);

        // Act
        consumer.Handle(new UserRegisteredEvent(42, "Alice"));

        // Assert - the entry is rendered from the template, so both values must appear
        var entry = logger.LatestEntry;
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry.Message).Contains("UserId=42");
        await Assert.That(entry.Message).Contains("Username=Alice");
        await Assert.That(entry.LogLevel).IsEqualTo(LogLevel.Information);
    }

    [Test]
    public async Task Handle_WhenNotificationIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = Mock.Logger<UserRegisteredEventConsumer>();
        var consumer = new UserRegisteredEventConsumer(logger);

        // Act & Assert
        await Assert.That(() => consumer.Handle(null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("notification");

        // A rejected notification must not produce a log entry
        logger.VerifyNoLogs();
    }
}
