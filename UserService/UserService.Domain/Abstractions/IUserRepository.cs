using UserService.Domain.Entities;

namespace UserService.Domain.Abstractions;

/// <summary>
/// Domain-defined repository contract for User aggregate.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User> InsertAsync(User entity, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
}
