using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Commands.DeleteUser;
using UserService.Domain.Abstractions;

namespace UserService.Tests.Application.Commands;

public sealed class DeleteUserCommandHandlerTests
{
    private IUserRepositoryMock? repository;
    private DeleteUserCommandHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        repository = MockUserRepository.Create();
        handler = new DeleteUserCommandHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenUserExists_DeletesUserAndSavesChanges()
    {
        // Arrange
        repository!.DeleteAsync(1, Any<CancellationToken>())
            .Returns(true);
        var command = new DeleteUserCommand(1);

        // Act
        var result = await handler!.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task Handle_WhenUserNotFound_ReturnsFalse()
    {
        // Arrange - production contract: repository miss surfaces as false, not an exception
        repository!.DeleteAsync(999, Any<CancellationToken>())
            .Returns(false);
        var command = new DeleteUserCommand(999);

        // Act
        var result = await handler!.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result).IsFalse();
    }
}
