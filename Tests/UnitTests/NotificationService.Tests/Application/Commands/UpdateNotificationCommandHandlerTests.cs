using NotificationService.Application.Commands.UpdateNotification;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class UpdateNotificationCommandHandlerTests
{
    [Test]
    public async Task Constructor_WhenRepositoryIsNull_ThrowsArgumentNullException()
    {
        await Assert.That(() => new UpdateNotificationCommandHandler(null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("repository");
    }

    [Test]
    public async Task Handle_WhenNotificationDoesNotExist_ReturnsNull()
    {
        // Arrange
        var repository = MockNotificationRepository.Create();
        var handler = new UpdateNotificationCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(
            new UpdateNotificationCommand { Id = 999, Title = "New", Content = "New body" },
            CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();

        // No write may happen when the target does not exist
        repository!.GetByIdAsync(999L, Any<CancellationToken>()).WasCalled(Times.Once);
        repository.UpdateAsync(Any<Notification>(), Any<CancellationToken>()).WasNeverCalled();
    }

    [Test]
    public async Task Handle_WhenNotificationExists_UpdatesNotificationAndReturnsDto()
    {
        // Arrange
        var existing = Notification.Create("Old Title", "Old Content");
        var repository = MockNotificationRepository.Create();
        repository!.GetByIdAsync(7L, Any<CancellationToken>()).Returns(existing);
        var handler = new UpdateNotificationCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(
            new UpdateNotificationCommand { Id = 7, Title = "New Title", Content = "New Content" },
            CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Title).IsEqualTo("New Title");
        await Assert.That(result.Content).IsEqualTo("New Content");
        // Update() must not change the status or the creation timestamp
        await Assert.That(result.Status).IsEqualTo(nameof(NotificationStatus.Draft));

        // The loaded entity — not a fresh one — must be the one written back
        repository.GetByIdAsync(7L, Any<CancellationToken>()).WasCalled(Times.Once);
        repository.UpdateAsync(Any<Notification>(), Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
