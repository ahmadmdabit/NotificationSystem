using NotificationService.Application.Commands.CreateNotification;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace NotificationService.Tests.Application.Commands;

public class CreateNotificationCommandValidatorTests
{
    [Test]
    public async Task Validate_WhenCommandIsValid_ReturnsValid()
    {
        // Arrange
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = "Valid Title", Content = "Valid Content" };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_WhenTitleIsEmpty_ReturnsValidationError(string? invalidTitle)
    {
        // Arrange
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = invalidTitle!, Content = "Valid Content" };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        // The client-facing message is part of the contract, not incidental
        await Assert.That(result.Errors).Contains(e => e.PropertyName == "Title");
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "Title is required.");
    }

    [Test]
    public async Task Validate_WhenTitleExceeds200Characters_ReturnsValidationError()
    {
        // Arrange — 201 characters: one over the limit
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = new string('x', 201), Content = "Valid Content" };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Contains(e => e.PropertyName == "Title");
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "Title must not exceed 200 characters.");
    }

    [Test]
    public async Task Validate_WhenTitleIsExactly200Characters_IsValid()
    {
        // Boundary — the rule is MaximumLength(200), so 200 must pass
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = new string('x', 200), Content = "Valid Content" };

        var result = await validator.ValidateAsync(command);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_WhenContentIsEmpty_ReturnsValidationError(string? invalidContent)
    {
        // Arrange
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = "Valid Title", Content = invalidContent! };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Contains(e => e.PropertyName == "Content");
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "Content is required.");
    }

    [Test]
    public async Task Validate_WhenContentExceeds4000Characters_ReturnsValidationError()
    {
        // Arrange — 4001 characters: one over the limit
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = "Valid Title", Content = new string('x', 4001) };

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors).Contains(e => e.PropertyName == "Content");
        await Assert.That(result.Errors).Contains(e => e.ErrorMessage == "Content must not exceed 4000 characters.");
    }

    [Test]
    public async Task Validate_WhenContentIsExactly4000Characters_IsValid()
    {
        // Boundary — the rule is MaximumLength(4000), so 4000 must pass
        var validator = new CreateNotificationCommandValidator();
        var command = new CreateNotificationCommand { Title = "Valid Title", Content = new string('x', 4000) };

        var result = await validator.ValidateAsync(command);

        await Assert.That(result.IsValid).IsTrue();
    }
}