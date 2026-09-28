using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Commands.AuthenticateUser;

namespace UserService.Tests.Application.Commands;

public class AuthenticateUserCommandValidatorTests
{
    private AuthenticateUserCommandValidator? validator;

    [Before(HookType.Test)]
    public void SetUp()
    {
        validator = new AuthenticateUserCommandValidator();
    }

    [Test]
    public async Task Validate_WhenCommandIsValid_ReturnsValid()
    {
        // Arrange
        var command = new AuthenticateUserCommand
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
        var command = new AuthenticateUserCommand
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
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Validate_WhenPasswordIsEmpty_ReturnsError(string? invalidPassword)
    {
        // Arrange
        var command = new AuthenticateUserCommand
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
