using NotificationService.Application.Commands.SendNotifications;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class SendNotificationsCommandValidatorTests
{
    [Test]
    public async Task Validate_WhenBatchIsValid_ReturnsSuccess()
    {
        // Arrange
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand
        {
            Items =
            [
                new SendNotificationItem { NotificationId = 1, UserId = 10 },
                new SendNotificationItem { NotificationId = 2, UserId = 20 }
            ]
        };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task Validate_WhenItemsEmpty_ReturnsValidationError()
    {
        // Arrange
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand { Items = [] };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "At least one recipient item is required.");
    }

    [Test]
    public async Task Validate_WhenItemsNull_ReturnsValidationError()
    {
        // The .Must() rule guards null explicitly — a null Items must not throw
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand { Items = null! };

        var result = await validator.ValidateAsync(command);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Validate_WhenBatchExceeds1000Items_ReturnsValidationError()
    {
        // Arrange — 1001 items: one over the cap
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand
        {
            Items = Enumerable.Range(1, 1001)
                .Select(i => new SendNotificationItem { NotificationId = i, UserId = i })
                .ToList()
        };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "Batch may not exceed 1000 recipient items.");
    }

    [Test]
    public async Task Validate_WhenBatchIsExactly1000Items_IsValid()
    {
        // Boundary — the rule is Count() <= 1000, so 1000 must pass
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand
        {
            Items = Enumerable.Range(1, 1000)
                .Select(i => new SendNotificationItem { NotificationId = i, UserId = i })
                .ToList()
        };

        var result = await validator.ValidateAsync(command);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(0, 10)]
    [Arguments(-1, 10)]
    [Arguments(1, 0)]
    [Arguments(1, -5)]
    public async Task Validate_WhenItemContainsNonPositiveIds_ReturnsValidationError(long notificationId, long userId)
    {
        // Arrange
        var validator = new SendNotificationsCommandValidator();
        var command = new SendNotificationsCommand
        {
            Items = [new SendNotificationItem { NotificationId = notificationId, UserId = userId }]
        };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert — the child rule must fire on the offending property
        await Assert.That(result.IsValid).IsFalse();
        var expected = notificationId <= 0 ? "NotificationId" : "UserId";
        await Assert.That(result.Errors).Contains(e => e.PropertyName.Contains(expected));
    }
}