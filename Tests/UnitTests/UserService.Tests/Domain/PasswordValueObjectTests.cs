using TestDoubles.Mocks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Domain.Abstractions;
using UserService.Domain.ValueObjects;

namespace UserService.Tests.Domain;

public class PasswordValueObjectTests
{
    private IPasswordHasherMock? hasher;

    [Before(HookType.Test)]
    public void SetUp()
    {
        hasher = MockSecurityServices.CreateHasher(out _, out _);
    }


    [Test]
    public async Task Create_ComputesHashAndSaltFromHasher()
    {
        // Arrange
        var hasher = this.hasher!;
        var passwordText = "MySecurePassword123";

        // Act
        var password = Password.Create(passwordText, hasher);

        // Assert
        await Assert.That(password).IsNotNull();
        await Assert.That(password.Hash).IsNotNull();
        await Assert.That(password.Salt).IsNotNull();
        await Assert.That(password.Hash.Length).IsGreaterThan(0);
        await Assert.That(password.Salt.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Rehydrate_WithValidBuffers_AssignsPropertiesDirectly()
    {
        // Arrange
        var hash = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var salt = new byte[] { 0x50, 0x60, 0x70, 0x80 };

        // Act
        var password = Password.Rehydrate(hash, salt);

        // Assert
        await Assert.That(password.Hash).IsEqualTo(hash);
        await Assert.That(password.Salt).IsEqualTo(salt);
    }

    [Test]
    public void Rehydrate_WhenHashIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var salt = new byte[] { 0x01, 0x02 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Password.Rehydrate(null!, salt));
    }

    [Test]
    public void Rehydrate_WhenSaltIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var hash = new byte[] { 0x01, 0x02 };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => Password.Rehydrate(hash, null!));
    }
}
