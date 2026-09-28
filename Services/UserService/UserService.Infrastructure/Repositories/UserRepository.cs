using System.Data;

using Dapper;

using Shared.Application.Abstractions;

using UserService.Domain.Abstractions;
using UserService.Domain.Entities;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based User repository implementing IUserRepository.
/// Write operations use stored procedures; read operations use Dapper QueryAsync.
/// </summary>
/// <remarks>
/// The SQL contract is built by <see cref="UserCommandFactory"/> so it can be verified directly;
/// this type only executes. Dapper's query methods are static extension methods on
/// <see cref="IDbConnection"/> and cannot be intercepted by a test double, so the commands are
/// the observable unit here.
/// </remarks>
public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnection connection;
    private readonly IUnitOfWork unitOfWork;

    public UserRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var cmd = UserCommandFactory.GetById(id, unitOfWork.Transaction, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);
        return row is null ? null : ToEntity(row);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var cmd = UserCommandFactory.GetByUsername(username, unitOfWork.Transaction, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);
        return row is null ? null : ToEntity(row);
    }

    public async Task<User> InsertAsync(User entity, CancellationToken cancellationToken = default)
    {
        var cmd = UserCommandFactory.Insert(entity, unitOfWork.Transaction, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);

        return row is null
            ? throw new InvalidOperationException("Failed to insert user.")
            : ToEntity(row);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var cmd = UserCommandFactory.Delete(id, unitOfWork.Transaction, cancellationToken);
        return await connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cmd = UserCommandFactory.GetAll(unitOfWork.Transaction, cancellationToken);
        var rows = await connection.QueryAsync<UserRow>(cmd).ConfigureAwait(false);
        return rows.Select(ToEntity).ToList();
    }

    /// <summary>Hydration DTO: raw credential columns; never leaves the repository.</summary>
    private sealed class UserRow
    {
        public long Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public byte[] PasswordHash { get; set; } = [];
        public byte[] PasswordSalt { get; set; } = [];
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private static User ToEntity(UserRow row)
        => User.Rehydrate(row.Id, row.Username, row.PasswordHash, row.PasswordSalt, row.CreatedAt, row.UpdatedAt);
}