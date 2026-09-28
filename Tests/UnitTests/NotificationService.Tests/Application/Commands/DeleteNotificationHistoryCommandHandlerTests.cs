using NotificationService.Application.Commands.DeleteNotificationHistory;
using NotificationService.Domain;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class DeleteNotificationHistoryCommandHandlerTests
{
    [Test]
    public async Task Handle_WhenRecordExists_DeletesAndReturnsTrue()
    {
        // Arrange
        var repository = MockNotificationHistoryRepository.Create(
            new NotificationHistory { NotificationId = 1, UserId = 10 });
        var handler = new DeleteNotificationHistoryCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(new DeleteNotificationHistoryCommand(1, 10), CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
        // Both halves of the composite key must reach the repository
        repository!.DeleteAsync(1L, 10L, Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenRecordDoesNotExist_ReturnsFalse()
    {
        // Arrange — repository seeded with a different key, so this one is absent
        var repository = MockNotificationHistoryRepository.Create(
            new NotificationHistory { NotificationId = 1, UserId = 10 });
        var handler = new DeleteNotificationHistoryCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(new DeleteNotificationHistoryCommand(999, 888), CancellationToken.None);

        // Assert
        await Assert.That(result).IsFalse();
        repository!.DeleteAsync(999L, 888L, Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
