using NotificationService.Application.Queries.GetNotificationById;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Queries;

public class GetNotificationByIdQueryHandlerTests
{
    private INotificationRepositoryMock? repository;
    private GetNotificationByIdQueryHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        repository = MockNotificationRepository.Create();
        handler = new GetNotificationByIdQueryHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenNotificationExists_ReturnsNotificationDto()
    {
        // Arrange
        var notification = Notification.Create("Title", "Content");
        repository!.GetByIdAsync(42L, Any<CancellationToken>()).Returns(notification);

        // Act
        var result = await handler!.Handle(new GetNotificationByIdQuery(42), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Title).IsEqualTo("Title");
        await Assert.That(result.Content).IsEqualTo("Content");
        await Assert.That(result.Status).IsEqualTo("Draft");
        await Assert.That(result.CreatedAt).IsEqualTo(notification.CreatedAt);

        // The id the query carried must reach the repository
        repository!.GetByIdAsync(42L, Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenNotificationDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await handler!.Handle(new GetNotificationByIdQuery(999), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();

        repository!.GetByIdAsync(999L, Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
