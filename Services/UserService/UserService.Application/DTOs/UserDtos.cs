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
/// <remarks>
/// Every member is settable and assigned by the mapper. A get-only property here would still be
/// serialised by System.Text.Json and would ship its <c>default</c> value on the wire — which is
/// how a dead <c>UtcNow</c> property ended up emitting <c>0001-01-01T00:00:00</c> in every
/// registration response (F-03). Do not add read-only members to a serialised DTO.
/// </remarks>
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
