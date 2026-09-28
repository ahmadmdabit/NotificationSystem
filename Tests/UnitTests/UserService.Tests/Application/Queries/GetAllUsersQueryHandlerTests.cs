using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Core;

using UserService.Application.Queries.GetAllUsers;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Tests.Application.Queries;

public class GetAllUsersQueryHandlerTests
{
    private IUserRepositoryMock? repository;
    private GetAllUsersQueryHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        // Default: an empty repository, so the "no users" case needs no extra arrangement.
        repository = MockUserRepository.Create();
        handler = new GetAllUsersQueryHandler(repository.Object);
    }

    [Test]
    public async Task Handle_WhenNoUsersExist_ReturnsEmptyList()
    {
        // Act
        var result = await handler!.Handle(new GetAllUsersQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task Handle_WhenUsersExist_ReturnsMappedDtos()
    {
        // Arrange — seed two users; Rehydrate takes explicit ids and dates, hardcoded hash/salt.
        var hash = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var salt = new byte[] { 0x05, 0x06, 0x07, 0x08 };
        var user1 = User.Rehydrate(1, "User1", hash, salt, DateTime.UtcNow, DateTime.UtcNow);
        var user2 = User.Rehydrate(2, "User2", hash, salt, DateTime.UtcNow, DateTime.UtcNow);
        repository!.GetAllAsync(Any<CancellationToken>())
            .Returns(new List<User> { user1, user2 });

        // Act
        var result = await handler!.Handle(new GetAllUsersQuery(), CancellationToken.None);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).Count().IsEqualTo(2);

        // Direct collection assertions report the offending element on failure;
        // result.Any(...).IsTrue() would only say "expected true but was false".
        await Assert.That(result).Contains(u => u.Username == "User1");
        await Assert.That(result).Contains(u => u.Username == "User2");
        await Assert.That(result).All(u => u.Id > 0);
        await Assert.That(result).All(u => u.CreatedAt is not null);

        repository!.GetAllAsync(Any<CancellationToken>()).WasCalled(Times.Once);
    }
}
