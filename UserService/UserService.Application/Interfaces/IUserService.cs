using UserService.Application.DTOs;

namespace UserService.Application.Interfaces;

/// <summary>
/// Application service contract for User operations.
/// </summary>
public interface IUserService
{
    Task<UserDto> RegisterAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<AuthenticateResultDto?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<UserReadDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserReadDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
