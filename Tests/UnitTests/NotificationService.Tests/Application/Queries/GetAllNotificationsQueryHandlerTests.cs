using NotificationService.Application.Queries.GetAllNotifications;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Queries;

public class GetAllNotificationsQueryHandlerTests
{
    private INotificationRepositoryMock? repository;
    private GetAllNotificationsQueryHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        // Default: empty repository, so the "no notifications" case needs no extra arrangement.
        repository = MockNotificationRepository.Create();
        handler = new GetAllNotificationsQueryHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenNoNotificationsExist_ReturnsEmptyList()
    {
        // Act
        var result = await handler!.Handle(new GetAllNotificationsQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Handle_WhenNotificationsExist_MapsEveryNotificationToDto()
    {
        // Arrange
        repository!.GetAllAsync(Any<CancellationToken>()).Returns(
        [
            Notification.Create("First", "Body one"),
            Notification.Create("Second", "Body two")
        ]);

        // Act
        var result = await handler!.Handle(new GetAllNotificationsQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
        await Assert.That(result).Contains(d => d.Title == "First");
        await Assert.That(result).Contains(d => d.Title == "Second");
        // Notification.Create always starts in Draft
        await Assert.That(result).All(d => d.Status == "Draft");

        repository!.GetAllAsync(Any<CancellationToken>()).WasCalled(Times.Once);
    }
}