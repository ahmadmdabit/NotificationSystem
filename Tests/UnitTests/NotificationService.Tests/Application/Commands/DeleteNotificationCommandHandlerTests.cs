using NotificationService.Application.Commands.DeleteNotification;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class DeleteNotificationCommandHandlerTests
{
    [Test]
    public async Task Handle_WhenNotificationExists_DeletesAndReturnsTrue()
    {
        // Arrange
        var repository = MockNotificationRepository.Create();
        repository.DeleteAsync(5L, Any<CancellationToken>()).Returns(true);
        var handler = new DeleteNotificationCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(new DeleteNotificationCommand(5), CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
        // The id the command carried must reach the repository
        repository!.DeleteAsync(5L, Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenNotificationDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var repository = MockNotificationRepository.Create();
        repository.DeleteAsync(999L, Any<CancellationToken>()).Returns(false);
        var handler = new DeleteNotificationCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(new DeleteNotificationCommand(999), CancellationToken.None);

        // Assert
        await Assert.That(result).IsFalse();
        repository!.DeleteAsync(999L, Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
