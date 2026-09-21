namespace UserService.Domain.ValueObjects;

/// <summary>
/// Value object encapsulating password hash and salt.
/// </summary>
public sealed class Password
{
    public byte[] Hash { get; }
    public byte[] Salt { get; }

    private Password(byte[] hash, byte[] salt)
    {
        Hash = hash;
        Salt = salt;
    }

    public static Password Create(string plainText, Abstractions.IPasswordHasher hasher)
    {
        hasher.HashPassword(plainText, out var hash, out var salt);
        return new Password(hash, salt);
    }
}
