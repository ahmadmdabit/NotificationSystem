namespace UserService.Domain.Abstractions;

/// <summary>
/// Domain service interface for password hashing.
/// </summary>
public interface IPasswordHasher
{
    void HashPassword(string password, out byte[] hash, out byte[] salt);
    bool VerifyPassword(string password, byte[] hash, byte[] salt);
}
