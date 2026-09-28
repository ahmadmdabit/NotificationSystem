using TestDoubles.Mocks;
using TestDoubles.Stubs;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Tests.Domain;

public class UserAggregateTests
{
    private IPasswordHasherMock? hasher;

    [Before(HookType.Test)]
    public void SetUp()
    {
        hasher = MockSecurityServices.CreateHasher(out _, out _, verifyAlwaysSucceeds: true);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public void Create_WhenUsernameIsInvalid_ThrowsArgumentException(string? invalidUsername)
    {
        // Arrange
        var hasher = this.hasher!;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => User.Create(invalidUsername!, "ValidPassword123", hasher));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public void Create_WhenPasswordIsInvalid_ThrowsArgumentException(string? invalidPassword)
    {
        // Arrange
        var hasher = this.hasher!;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => User.Create("ValidUsername", invalidPassword!, hasher));
    }

    [Test]
    public async Task Create_WhenValid_InstantiatesUserWithHashedPasswordAndUtcTimestamp()
    {
        // Arrange
        var hasher = this.hasher!;

        // Act
        var user = User.Create("TestUser", "MyPassword123", hasher);

        // Assert
        await Assert.That(user.Username).IsEqualTo("TestUser");
        await Assert.That(user.Password).IsNotNull();
        await Assert.That(user.Password.Hash).IsNotNull();
        await Assert.That(user.Password.Salt).IsNotNull();
        await Assert.That(user.CreatedAt).IsNotNull();
        await Assert.That(user.CreatedAt.Value.Kind).IsEqualTo(DateTimeKind.Utc);
        await Assert.That(user.Id).IsEqualTo(0); // Not yet persisted
    }

    [Test]
    public async Task Rehydrate_WithValidParameters_RestoresUserWithoutRehashing()
    {
        // Arrange
        var id = 42L;
        var username = "RehydratedUser";
        var hash = new byte[] { 0x01, 0x02, 0x03 };
        var salt = new byte[] { 0x04, 0x05, 0x06 };
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var user = User.Rehydrate(id, username, hash, salt, createdAt, updatedAt);

        // Assert
        await Assert.That(user.Id).IsEqualTo(id);
        await Assert.That(user.Username).IsEqualTo(username);
        await Assert.That(user.Password.Hash).IsEqualTo(hash);
        await Assert.That(user.Password.Salt).IsEqualTo(salt);
        await Assert.That(user.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(user.UpdatedAt).IsEqualTo(updatedAt);
    }

    [Test]
    public void Rehydrate_WhenAnyRequiredParameterIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var id = 1L;
        var username = "TestUser";
        var hash = new byte[] { 0x01 };
        var salt = new byte[] { 0x02 };
        var createdAt = DateTime.UtcNow;
        var updatedAt = DateTime.UtcNow;

        // Act & Assert - username null
        Assert.Throws<ArgumentNullException>(() => User.Rehydrate(id, null!, hash, salt, createdAt, updatedAt));

        // Act & Assert - hash null
        Assert.Throws<ArgumentNullException>(() => User.Rehydrate(id, username, null!, salt, createdAt, updatedAt));

        // Act & Assert - salt null
        Assert.Throws<ArgumentNullException>(() => User.Rehydrate(id, username, hash, null!, createdAt, updatedAt));
    }

    [Test]
    public async Task VerifyPassword_WhenPasswordMatches_DoesNotThrow()
    {
        // Arrange
        var hasher = this.hasher!;
        var user = User.Create("TestUser", "CorrectPassword", hasher);

        // Act & Assert - should not throw
        await Assert.That(() => user.VerifyPassword("CorrectPassword", hasher))
            .ThrowsNothing();
    }

    [Test]
    public async Task VerifyPassword_WhenPasswordMismatch_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var hasher = MockSecurityServices.CreateHasher(out _, out _, verifyAlwaysSucceeds: false);
        var user = User.Create("TestUser", "CorrectPassword", hasher);

        // Act & Assert
        Assert.Throws<UnauthorizedAccessException>(() => user.VerifyPassword("WrongPassword", hasher));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public void UpdatePassword_WhenNewPasswordIsInvalid_ThrowsArgumentException(string? invalidPassword)
    {
        // Arrange
        var hasher = this.hasher!;
        var user = User.Create("TestUser", "OriginalPassword", hasher);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => user.UpdatePassword(invalidPassword!, hasher));
    }

    [Test]
    public async Task UpdatePassword_WhenValid_UpdatesPasswordAndTimestamp()
    {
        // Arrange
        var hasher = this.hasher!;
        var user = User.Create("TestUser", "OriginalPassword", hasher);
        var originalUpdatedAt = user.UpdatedAt;

        // Act
        user.UpdatePassword("NewPassword123", hasher);

        // Assert
        await Assert.That(user.Password).IsNotNull();
        await Assert.That(user.UpdatedAt).IsNotNull();
        await Assert.That(user.UpdatedAt.Value).IsGreaterThan(originalUpdatedAt ?? DateTime.MinValue);
    }

    [Test]
    public async Task DomainEvents_CanBeAddedAndCleared()
    {
        // Arrange
        var user = User.Create("TestUser", "Password123", hasher!);
        var evt = TestDomainEventFactory.Create("TestUser");

        // Act - add event
        user.AddDomainEvent(evt);

        // Assert - event added
        await Assert.That(user.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(user.DomainEvents.First()).IsEqualTo(evt);

        // Act - clear events
        user.ClearDomainEvents();

        // Assert - events cleared
        await Assert.That(user.DomainEvents).IsEmpty();
    }
}
