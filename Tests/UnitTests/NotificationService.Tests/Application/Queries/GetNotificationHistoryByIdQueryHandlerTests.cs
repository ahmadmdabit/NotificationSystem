using NotificationService.Application.Queries.GetNotificationHistoryById;
using NotificationService.Domain;
using NotificationService.Domain.Abstractions;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Queries;

public class GetNotificationHistoryByIdQueryHandlerTests
{
    private INotificationHistoryRepositoryMock? repository;
    private GetNotificationHistoryByIdQueryHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        repository = MockNotificationHistoryRepository.Create();
        handler = new GetNotificationHistoryByIdQueryHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenHistoryExists_ReturnsDto()
    {
        // Arrange
        var createdAt = new DateTime(2026, 4, 2, 8, 30, 0, DateTimeKind.Utc);
        repository!.GetByIdAsync(1L, 10L, Any<CancellationToken>()).Returns(
            new NotificationHistory { NotificationId = 1, UserId = 10, CreatedAt = createdAt });

        // Act
        var result = await handler!.Handle(new GetNotificationHistoryByIdQuery(1, 10), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.NotificationId).IsEqualTo(1);
        await Assert.That(result.UserId).IsEqualTo(10);
        await Assert.That(result.CreatedAt).IsEqualTo(createdAt);

        // Both halves of the composite key must reach the repository
        repository!.GetByIdAsync(1L, 10L, Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenHistoryDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await handler!.Handle(new GetNotificationHistoryByIdQuery(999, 888), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();

        repository!.GetByIdAsync(999L, 888L, Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
