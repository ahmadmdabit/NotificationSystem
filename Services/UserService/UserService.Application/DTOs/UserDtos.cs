namespace UserService.Application.DTOs;

/// <summary>
/// User data transfer object for API responses (excludes sensitive fields).
/// </summary>
public sealed class UserReadDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// User data transfer object for write operations.
/// </summary>
public sealed class UserDto
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Authentication result DTO.
/// </summary>
public sealed class AuthenticateResultDto
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
