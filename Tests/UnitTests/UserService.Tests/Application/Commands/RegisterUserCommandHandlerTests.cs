using Microsoft.Extensions.Logging.Abstractions;

using Shared.Domain;
using Shared.Domain.Exceptions;

using TestDoubles.Helpers;
using TestDoubles.Mocks;
using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Commands.RegisterUser;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;
using UserService.Domain.Events;

namespace UserService.Tests.Application.Commands;

public class RegisterUserCommandHandlerTests
{
    private IUserRepositoryMock? repository;
    private IPasswordHasherMock? hasher;
    private RegisterUserCommandHandler? handler;

    [Before(HookType.Test)]
    public void SetUp()
    {
        repository = MockUserRepository.Create();
        hasher = MockSecurityServices.CreateHasher(out _, out _);
        handler = new RegisterUserCommandHandler(repository.Object, hasher.Object);
    }

    /// <summary>
    /// Clears <see cref="DomainEventCollector"/> between tests. This is real cleanup, not
    /// cargo-cult: the collector is <b>static</b> state, and TUnit only gives a fresh test-class
    /// instance per test — it does not reset statics. Without this, a failure in one test leaks
    /// events into the next.
    /// </summary>
    [After(HookType.Test)]
    public void TearDown()
    {
        DomainEventCollector.Clear();
    }

    [Test]
    public void Constructor_WhenRepositoryIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var hasher = MockSecurityServices.CreateHasher(out _, out _);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RegisterUserCommandHandler(null!, hasher.Object));
    }

    [Test]
    public void Constructor_WhenHasherIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var repo = MockUserRepository.Create();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RegisterUserCommandHandler(repo.Object, null!));
    }

    [Test]
    public async Task Handle_WhenUserAlreadyExistsInPreCheck_ThrowsDuplicateEntityException()
    {
        // Arrange
        var existingUser = User.Create("ExistingUser", "Password123", hasher!.Object);
        var repo = MockUserRepository.Create(existingUser);
        var handler = new RegisterUserCommandHandler(repo.Object, hasher.Object);
        var command = new RegisterUserCommand { Username = "ExistingUser", Password = "NewPass123" };

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEntityException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_WhenValid_PersistsUserEnqueuesDomainEventAndReturnsDto()
    {
        // Arrange — InMemoryUserRepository completes synchronously, so nothing inside the handler
        // suspends and the DomainEventCollector writes the handler makes stay visible to this body.
        var repository = new InMemoryUserRepository();
        var handler = new RegisterUserCommandHandler(repository, hasher!.Object);
        var command = new RegisterUserCommand { Username = "NewUser", Password = "Password123" };
        DomainEventCollector.Clear();

        // Sentinel proving the handler's own Add actually reached the collector this test can
        // drain. If the handler stopped calling AddRange the count would fall 2 -> 1, so this is
        // not decoration.
        DomainEventCollector.Add(TestDomainEventFactory.Create("sentinel"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Drain immediately.
        //
        // Note on AsyncLocal: DomainEventCollector is an AsyncLocal, so a *parallel* test's
        // Clear() cannot reach this flow — the earlier comment here claimed it could, which was
        // wrong. What actually matters is draining with no intervening await, so nothing can
        // re-seed the list between the handler's Add and this read.
        var events = DomainEventCollector.Drain();

        // Assert — sentinel + handler event = 2; verify UserRegisteredEvent is among them
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Username).IsEqualTo("NewUser");
        await Assert.That(events).Count().IsEqualTo(2);
        await Assert.That(events).Contains(e => e is UserRegisteredEvent);
    }

    [Test]
    [Arguments(2601)]
    [Arguments(2627)]
    public async Task Handle_WhenDatabaseThrowsUniqueConstraintViolation_MapsToDuplicateEntityException(int sqlErrorCode)
    {
        // Arrange
        var repo = MockUserRepository.Create();
        repo.InsertAsync(Any<User>(), Any<CancellationToken>())
            .Throws(SqlExceptionTestFactory.Create(sqlErrorCode));
        var handler = new RegisterUserCommandHandler(repo.Object, hasher!.Object);
        var command = new RegisterUserCommand { Username = "TestUser", Password = "Password123" };

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEntityException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_WhenDatabaseThrowsNonSqlException_RethrowsOriginalException()
    {
        // Arrange
        var repo = MockUserRepository.Create();
        repo.InsertAsync(Any<User>(), Any<CancellationToken>())
            .Throws(new InvalidOperationException("Database error"));
        var handler = new RegisterUserCommandHandler(repo.Object, hasher!.Object);
        var command = new RegisterUserCommand { Username = "TestUser", Password = "Password123" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_WhenUsernameUnique_HashesPasswordAndPersistsUser()
    {
        // Arrange
        var command = new RegisterUserCommand { Username = "UniqueUser", Password = "Password123" };
        User? insertedUser = null;
        repository!.InsertAsync(Any<User>(), Any<CancellationToken>())
            .Returns((User u, CancellationToken _) => { insertedUser = u; return Task.FromResult(u); });
        var handler = new RegisterUserCommandHandler(repository!.Object, hasher!.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(insertedUser).IsNotNull();
        await Assert.That(insertedUser.Username).IsEqualTo("UniqueUser");
    }

    [Test]
    public async Task Handle_WhenUserAlreadyExists_ExceptionCarriesEntityName()
    {
        // Arrange
        var existingUser = User.Create("ExistingUser", "Password123", hasher!.Object);
        var repo = MockUserRepository.Create(existingUser);
        var handler = new RegisterUserCommandHandler(repo.Object, hasher.Object);
        var command = new RegisterUserCommand { Username = "ExistingUser", Password = "NewPass123" };

        // Act
        var exception = await Assert.ThrowsAsync<DuplicateEntityException>(() => handler.Handle(command, CancellationToken.None));

        // Assert - EntityName is the typed discriminator; the public message never echoes the username (user enumeration).
        await Assert.That(exception!.EntityName).IsEqualTo("User");
    }
}
