using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Queries.GetUserById;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Tests.Application.Queries;

public class GetUserByIdQueryHandlerTests
{
    private IUserRepositoryMock? repository;
    private GetUserByIdQueryHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        // Default: an empty repository, so the "not found" case needs no extra arrangement.
        repository = MockUserRepository.Create();
        handler = new GetUserByIdQueryHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenUserExists_ReturnsUserReadDto()
    {
        // Arrange — seed the user; Rehydrate takes an explicit id and dates, hardcoded hash/salt.
        var hash = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var salt = new byte[] { 0x05, 0x06, 0x07, 0x08 };
        var user = User.Rehydrate(1, "ExistingUser", hash, salt, DateTime.UtcNow, DateTime.UtcNow);
        repository!.GetByIdAsync(user.Id, Any<CancellationToken>())
            .Returns(user);
        var query = new GetUserByIdQuery(user.Id);

        // Act
        var result = await handler!.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Username).IsEqualTo("ExistingUser");
        await Assert.That(result.Id).IsEqualTo(user.Id);
        await Assert.That(result.CreatedAt).IsNotNull();
        await Assert.That(result.UpdatedAt).IsNotNull();

        repository!.GetByIdAsync(user.Id, Any<CancellationToken>()).WasCalled(Times.Once);
    }

    [Test]
    public async Task Handle_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var query = new GetUserByIdQuery(999);

        // Act
        var result = await handler!.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result).IsNull();
    }
}
