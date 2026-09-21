namespace UserService.Domain.Abstractions;

/// <summary>
/// Domain service interface for JWT token generation.
/// </summary>
public interface ITokenService
{
    string GenerateToken(long userId);
}
