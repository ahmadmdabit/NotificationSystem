using System.Security.Cryptography;

using UserService.Domain.Abstractions;

namespace UserService.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA512 password hasher implementing IPasswordHasher.
/// 600k iterations, 256-bit salt, 512-bit hash. Constant-time verification.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltByteSize = 32;       // 256-bit salt
    private const int HashByteSize = 64;       // 512-bit hash
    private const int Iterations = 600000;

    public void HashPassword(string password, out byte[] hash, out byte[] salt)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));

        using var rng = RandomNumberGenerator.Create();
        salt = new byte[SaltByteSize];
        rng.GetBytes(salt);

        hash = new byte[HashByteSize];
        Rfc2898DeriveBytes.Pbkdf2(password, salt, hash, Iterations, HashAlgorithmName.SHA512);
    }

    public bool VerifyPassword(string password, byte[] hash, byte[] salt)
    {
        if (password is null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        if (hash is null || hash.Length != HashByteSize)
            throw new ArgumentException($"Invalid hash length ({HashByteSize} bytes expected).", nameof(hash));
        if (salt is null || salt.Length != SaltByteSize)
            throw new ArgumentException($"Invalid salt length ({SaltByteSize} bytes expected).", nameof(salt));

        var computedHash = new byte[HashByteSize];
        Rfc2898DeriveBytes.Pbkdf2(password, salt, computedHash, Iterations, HashAlgorithmName.SHA512);
        return CryptographicOperations.FixedTimeEquals(computedHash, hash);
    }
}
