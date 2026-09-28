using NotificationService.Domain.Entities;
using NotificationService.Domain.Events;
using NotificationService.Domain.ValueObjects;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Domain;

public class NotificationAggregateTests
{
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WhenTitleIsInvalid_ThrowsArgumentException(string? invalidTitle)
    {
        await Assert.That(() => Notification.Create(invalidTitle!, "valid content"))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("title");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WhenContentIsInvalid_ThrowsArgumentException(string? invalidContent)
    {
        await Assert.That(() => Notification.Create("valid title", invalidContent!))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("content");
    }

    [Test]
    public async Task Create_WhenValid_InstantiatesNotificationInDraftStatus()
    {
        // Act
        var notification = Notification.Create("Title", "Content");

        // Assert
        await Assert.That(notification.Title).IsEqualTo("Title");
        await Assert.That(notification.Content).IsEqualTo("Content");
        await Assert.That(notification.Status).IsEqualTo(NotificationStatus.Draft);
        await Assert.That(notification.CreatedAt).IsNotNull();
        await Assert.That(notification.SentAt).IsNull();
        await Assert.That(notification.DomainEvents).IsEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Update_WhenTitleIsInvalid_ThrowsArgumentException(string? invalidTitle)
    {
        var notification = Notification.Create("Title", "Content");

        await Assert.That(() => notification.Update(invalidTitle!, "new content"))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("title");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Update_WhenContentIsInvalid_ThrowsArgumentException(string? invalidContent)
    {
        var notification = Notification.Create("Title", "Content");

        await Assert.That(() => notification.Update("new title", invalidContent!))
            .ThrowsExactly<ArgumentException>()
            .WithParameterName("content");
    }

    [Test]
    public async Task Update_WhenValid_UpdatesFieldsAndTimestamp()
    {
        // Arrange
        var notification = Notification.Create("Title", "Content");
        var createdAt = notification.CreatedAt;

        // Act
        notification.Update("New Title", "New Content");

        // Assert
        await Assert.That(notification.Title).IsEqualTo("New Title");
        await Assert.That(notification.Content).IsEqualTo("New Content");
        await Assert.That(notification.UpdatedAt).IsNotNull();
        await Assert.That(notification.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task MarkAsSent_WhenRecipientListIsNull_ThrowsArgumentNullException()
    {
        var notification = Notification.Create("Title", "Content");

        await Assert.That(() => notification.MarkAsSent(null!))
            .ThrowsExactly<ArgumentNullException>()
            .WithParameterName("recipientUserIds");
    }

    [Test]
    public async Task MarkAsSent_WhenInDraftStatus_TransitionsToSentAndRaisesDomainEvent()
    {
        // Arrange
        var notification = Notification.Create("Title", "Content");

        // Act
        var transitioned = notification.MarkAsSent([10L, 20L]);

        // Assert
        await Assert.That(transitioned).IsTrue();
        await Assert.That(notification.Status).IsEqualTo(NotificationStatus.Sent);
        await Assert.That(notification.SentAt).IsNotNull();
        // Exactly one event — a second would be double-published post-commit
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
        var evt = notification.DomainEvents.Single() as NotificationSentEvent;
        await Assert.That(evt).IsNotNull();
    }

    [Test]
    public async Task MarkAsSent_WhenAlreadySent_IsIdempotentAndReturnsFalseWithoutRaisingSecondEvent()
    {
        // Arrange
        var notification = Notification.Create("Title", "Content");
        notification.MarkAsSent([1L]);

        // Act
        var transitioned = notification.MarkAsSent([2L]);

        // Assert
        await Assert.That(transitioned).IsFalse();
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
    }
}
