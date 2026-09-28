using NotificationService.Application.Mappings;
using NotificationService.Domain;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Mappings;

public class NotificationMappingTests
{
    [Test]
    public async Task ToDto_NotificationEntity_MapsAllProperties()
    {
        // Arrange
        var notification = Notification.Create("Title", "Content");
        notification.MarkAsSent([1L, 2L]);
        var sentAt = notification.SentAt;

        // Act
        var dto = notification.ToDto();

        // Assert
        await Assert.That(dto.Id).IsEqualTo(notification.Id);
        await Assert.That(dto.Title).IsEqualTo("Title");
        await Assert.That(dto.Content).IsEqualTo("Content");
        // Status is surfaced as a string, so it must be the enum name — not "1"
        await Assert.That(dto.Status).IsEqualTo(nameof(NotificationStatus.Sent));
        await Assert.That(dto.SentAt).IsEqualTo(sentAt);
        await Assert.That(dto.CreatedAt).IsEqualTo(notification.CreatedAt);
    }

    [Test]
    public async Task ToDto_NotificationEntity_WhenDraft_ReportsDraftAndNullSentAt()
    {
        // Arrange
        var notification = Notification.Create("Draft", "Not sent yet");

        // Act
        var dto = notification.ToDto();

        // Assert
        await Assert.That(dto.Status).IsEqualTo(nameof(NotificationStatus.Draft));
        await Assert.That(dto.SentAt).IsNull();
    }

    [Test]
    public async Task ToDto_NotificationHistory_MapsAllProperties()
    {
        // Arrange
        var createdAt = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        var history = new NotificationHistory
        {
            NotificationId = 77,
            UserId = 88,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddHours(1)
        };

        // Act
        var dto = history.ToDto();

        // Assert
        await Assert.That(dto.NotificationId).IsEqualTo(77);
        await Assert.That(dto.UserId).IsEqualTo(88);
        await Assert.That(dto.CreatedAt).IsEqualTo(createdAt);
    }
}
