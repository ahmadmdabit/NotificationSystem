using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Infrastructure.Services;

namespace UserService.Tests.Infrastructure;

public class PasswordHasherTests
{
    private PasswordHasher? hasher;

    [Before(HookType.Test)]
    public void SetUp()
    {
        hasher = new PasswordHasher();
    }

    [Test]
    public void HashPassword_WhenPasswordIsNull_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>("password", () =>
            hasher!.HashPassword(null!, out _, out _));

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void HashPassword_WhenPasswordIsBlank_ThrowsArgumentException(string blankPassword)
    {
        // Act & Assert — blank and whitespace take the separate IsNullOrWhiteSpace guard.
        Assert.ThrowsExactly<ArgumentException>("password", () =>
            hasher!.HashPassword(blankPassword, out _, out _));
    }

    [Test]
    public async Task HashPassword_WhenValid_ProducesExactSaltAndHashSizes()
    {
        // Arrange
        const int expectedSaltSize = 32;  // 256-bit
        const int expectedHashSize = 64;  // 512-bit

        // Act
        hasher!.HashPassword("ValidPassword123", out var hash, out var salt);

        // Assert
        await Assert.That(hash).IsNotNull();
        await Assert.That(salt).IsNotNull();
        await Assert.That(hash.Length).IsEqualTo(expectedHashSize);
        await Assert.That(salt.Length).IsEqualTo(expectedSaltSize);
    }

    [Test]
    public async Task VerifyPassword_WhenCredentialsMatch_ReturnsTrue()
    {
        // Arrange
        hasher!.HashPassword("CorrectPassword", out var hash, out var salt);

        // Act
        var result = hasher.VerifyPassword("CorrectPassword", hash, salt);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task VerifyPassword_WhenPasswordDiffers_ReturnsFalse()
    {
        // Arrange
        hasher!.HashPassword("CorrectPassword", out var hash, out var salt);

        // Act
        var result = hasher.VerifyPassword("WrongPassword", hash, salt);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task VerifyPassword_WhenSaltTampered_ReturnsFalse()
    {
        // ⚠️ NEGATIVE CONTROL — this is NOT evidence of a tamper check.
        // Rfc2898DeriveBytes derives the key from the salt, so flipping salt bits changes the
        // derived hash and FixedTimeEquals returns false whether or not any salt-integrity check
        // exists. The test would pass against a hasher that never inspected the salt's origin.
        // Arrange
        hasher!.HashPassword("CorrectPassword", out var hash, out var salt);
        var tamperedSalt = new byte[salt.Length];
        Array.Copy(salt, tamperedSalt, salt.Length);
        tamperedSalt[0] ^= 0xFF; // Flip bits

        // Act
        var result = hasher.VerifyPassword("CorrectPassword", hash, tamperedSalt);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public void VerifyPassword_WhenBufferLengthsInvalid_ThrowsArgumentException()
    {
        // Arrange
        hasher!.HashPassword("Password", out var hash, out var salt);
        var badHash = new byte[hash.Length + 1];
        var badSalt = new byte[salt.Length + 1];

        // Act & Assert - hash length invalid, named "hash"
        Assert.ThrowsExactly<ArgumentException>("hash", () => hasher.VerifyPassword("Password", badHash, salt));

        // Act & Assert - salt length invalid, named "salt"
        Assert.ThrowsExactly<ArgumentException>("salt", () => hasher.VerifyPassword("Password", hash, badSalt));
    }

    [Test]
    public void VerifyPassword_WhenPasswordIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        hasher!.HashPassword("Password", out var hash, out var salt);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>("password", () => hasher.VerifyPassword(null!, hash, salt));
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public void VerifyPassword_WhenPasswordIsWhiteSpace_ThrowsArgumentException(string whitespacePassword)
    {
        // Arrange
        hasher!.HashPassword("Password", out var hash, out var salt);

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>("password", () =>
            hasher.VerifyPassword(whitespacePassword, hash, salt));
    }
}
