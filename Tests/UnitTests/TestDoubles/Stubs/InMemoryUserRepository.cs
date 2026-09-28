using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace TestDoubles.Stubs;

/// <summary>
/// Hand-written synchronous in-memory IUserRepository (every member returns Task.FromResult).
/// TUnit.Mocks invocations suspend the async flow, which orphans AsyncLocal writes made inside
/// an awaited handler (DomainEventCollector) — with this stub the whole chain completes
/// synchronously, so collector writes made by the handler stay visible to the test body.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly Dictionary<long, User> store = [];
    private long nextId = 1;

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(store.GetValueOrDefault(id));

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Task.FromResult(store.Values.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));

    public Task<User> InsertAsync(User entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        // Check for duplicate username
        if (store.Values.Any(u => string.Equals(u.Username, entity.Username, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        // This used to assign the id by reflection with `idProp?.SetValue(...)`, which fails OPEN:
        // rename User.Id and every insert kept Id == 0, so _store[0] silently overwrote on each
        // insert and the consuming test (which asserts on Username, not id) still passed. Rehydrate
        // is the supported factory for a DB-assigned identity and now fails LOUDLY at compile time.
        var id = entity.Id == 0 ? nextId++ : entity.Id;
        var stored = User.Rehydrate(
            id,
            entity.Username,
            entity.Password.Hash,
            entity.Password.Salt,
            entity.CreatedAt,
            entity.UpdatedAt);

        store[id] = stored;
        return Task.FromResult(stored);
    }

    public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        => Task.FromResult(store.Remove(id));

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<User>>([.. store.Values]);
}
