using NotificationService.Application.Queries.GetNotificationHistory;
using NotificationService.Domain;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Queries;

public class GetNotificationHistoryQueryHandlerTests
{
    [Test]
    public async Task Handle_WhenEmpty_ReturnsEmptyList()
    {
        // Arrange
        var repository = MockNotificationHistoryRepository.Create();
        var handler = new GetNotificationHistoryQueryHandler(repository.Object);

        // Act
        var result = await handler.Handle(new GetNotificationHistoryQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEmpty();

        repository!.GetAllAsync(Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenRecordsExist_ReturnsMappedDtos()
    {
        // Arrange
        var createdAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var repository = MockNotificationHistoryRepository.Create(
            new NotificationHistory { NotificationId = 1, UserId = 10, CreatedAt = createdAt },
            new NotificationHistory { NotificationId = 2, UserId = 20, CreatedAt = createdAt });
        var handler = new GetNotificationHistoryQueryHandler(repository.Object);

        // Act
        var result = await handler.Handle(new GetNotificationHistoryQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).Count().IsEqualTo(2);
        // Composite key must survive the mapping intact
        await Assert.That(result).Contains(d => d.NotificationId == 1 && d.UserId == 10);
        await Assert.That(result).Contains(d => d.NotificationId == 2 && d.UserId == 20);
        await Assert.That(result).All(d => d.CreatedAt == createdAt);

        repository.GetAllAsync(Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
