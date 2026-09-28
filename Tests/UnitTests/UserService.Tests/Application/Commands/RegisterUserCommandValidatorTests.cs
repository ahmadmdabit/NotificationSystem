using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Commands.RegisterUser;

namespace UserService.Tests.Application.Commands;

public class RegisterUserCommandValidatorTests
{
    private RegisterUserCommandValidator? validator;

    [Before(HookType.Test)]
    public void SetUp()
    {
        validator = new RegisterUserCommandValidator();
    }

    [Test]
    public async Task Validate_WhenCommandIsValid_ReturnsSuccess()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Username = "ValidUser",
            Password = "ValidPass123"
        };

        // Act
        var result = await validator!.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_WhenUsernameIsEmpty_ReturnsError(string? invalidUsername)
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Username = invalidUsername!,
            Password = "ValidPass123"
        };

        // Act
        var result = await validator!.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Validate_WhenUsernameIsTooShort_ReturnsError()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Username = "Ab",
            Password = "ValidPass123"
        };

        // Act
        var result = await validator!.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Validate_WhenUsernameExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var longUsername = new string('A', 51);
        var command = new RegisterUserCommand
        {
            Username = longUsername,
            Password = "ValidPass123"
        };

        // Act
        var result = await validator!.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("1234567")]
    public async Task Validate_WhenPasswordIsUnderEightCharsOrEmpty_ReturnsError(string? invalidPassword)
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Username = "ValidUser",
            Password = invalidPassword!
        };

        // Act
        var result = await validator!.ValidateAsync(command);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
    }
}
