using Microsoft.Extensions.Options;

using Shared.Helpers;

using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Commands.AuthenticateUser;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Tests.Application.Commands;

public sealed class AuthenticateUserCommandHandlerTests
{
    private IUserRepositoryMock? repository;
    private IPasswordHasherMock? hasher;
    private ITokenServiceMock? tokenService;
    private AuthenticateUserCommandHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        repository = MockUserRepository.Create();
        hasher = MockSecurityServices.CreateHasher(out _, out _, verifyAlwaysSucceeds: true);
        tokenService = MockSecurityServices.CreateTokenService();
        var settings = Options.Create(new AppSettings
        {
            Secret = new string('a', 64),
            ServiceAccountUsername = "uiservice"
        });
        handler = new AuthenticateUserCommandHandler(
            repository.Object,
            hasher.Object,
            tokenService.Object,
            settings);
    }

    [Test]
    public async Task Handle_WhenCredentialsValid_ReturnsAuthResultWithToken()
    {
        // Arrange
        var user = User.Create("ValidUser", "Password123", hasher!.Object);
        var repo = MockUserRepository.Create(user);
        var settings = Options.Create(new AppSettings
        {
            Secret = new string('a', 64),
            ServiceAccountUsername = "uiservice"
        });
        var handler = new AuthenticateUserCommandHandler(
            repo.Object,
            hasher.Object,
            tokenService!.Object,
            settings);
        var command = new AuthenticateUserCommand
        {
            Username = "ValidUser",
            Password = "Password123"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Username).IsEqualTo("ValidUser");
        await Assert.That(result.Token).IsNotNull();
        await Assert.That(result.Token.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Handle_WhenUserNotFound_ReturnsNull()
    {
        // Arrange — production returns null (timing-safe dummy hash); does not throw
        var command = new AuthenticateUserCommand
        {
            Username = "UnknownUser",
            Password = "Password123"
        };

        // Act
        var result = await handler!.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Handle_WhenPasswordIncorrect_ReturnsNull()
    {
        // Arrange — VerifyPassword throws UnauthorizedAccessException; handler maps to null
        var failingHasher = MockSecurityServices.CreateHasher(out _, out _, verifyAlwaysSucceeds: false);
        var user = User.Create("ValidUser", "Password123", failingHasher.Object);
        var repo = MockUserRepository.Create(user);
        var settings = Options.Create(new AppSettings
        {
            Secret = new string('a', 64),
            ServiceAccountUsername = "uiservice"
        });
        var handler = new AuthenticateUserCommandHandler(
            repo.Object,
            failingHasher.Object,
            tokenService!.Object,
            settings);
        var command = new AuthenticateUserCommand
        {
            Username = "ValidUser",
            Password = "WrongPassword"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();
    }
}
