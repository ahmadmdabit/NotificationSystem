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

    /// <summary>
    /// Rehydrates the value object from persisted hash/salt without re-running PBKDF2.
    /// Used only at the persistence boundary (see <c>UserRepository.GetCredentialsAsync</c>
    /// + <c>User.Rehydrate</c>); never accepts plain text.
    /// </summary>
    public static Password Rehydrate(byte[] hash, byte[] salt)
    {
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(salt);
        return new Password(hash, salt);
    }
}
