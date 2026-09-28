using NotificationService.Application.Commands.CreateNotification;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class CreateNotificationCommandHandlerTests
{
    [Test]
    public async Task Handle_WhenValidCommand_PersistsNotificationAndReturnsDto()
    {
        // Arrange
        var repository = MockNotificationRepository.Create();
        var handler = new CreateNotificationCommandHandler(repository.Object);

        // Act
        var result = await handler.Handle(
            new CreateNotificationCommand { Title = "Title", Content = "Content" },
            CancellationToken.None);

        // Assert
        await Assert.That(result.Title).IsEqualTo("Title");
        await Assert.That(result.Content).IsEqualTo("Content");
        // A newly created notification is always Draft — the handler must not invent a status
        await Assert.That(result.Status).IsEqualTo(nameof(NotificationStatus.Draft));
        await Assert.That(result.CreatedAt).IsNotNull();

        // The entity built by the handler must be the one persisted
        repository!.InsertAsync(Any<Notification>(), Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
