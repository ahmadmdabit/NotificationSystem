using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based User repository implementing IUserRepository.
/// Write operations use stored procedures; read operations use Dapper QueryAsync.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnection _connection;

    public UserRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT Id, Username, CreatedAt, UpdatedAt FROM Users WHERE Id = @Id";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<User>(cmd).ConfigureAwait(false);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT Id, Username, CreatedAt, UpdatedAt FROM Users WHERE Username = @Username";
        var cmd = new CommandDefinition(sql, new { Username = username }, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<User>(cmd).ConfigureAwait(false);
    }

    public async Task<User> InsertAsync(User entity, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            entity.Username,
            entity.Password.Hash,
            entity.Password.Salt
        };

        var result = await _connection.QuerySingleOrDefaultAsync<User>(
            "sp_RegisterUser",
            parameters,
            commandType: System.Data.CommandType.StoredProcedure).ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Failed to insert user.");
    }

    public async Task<User> UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        var sql = "UPDATE Users SET Username = @Username WHERE Id = @Id OUTPUT INSERTED.*";
        var cmd = new CommandDefinition(sql, entity, cancellationToken: cancellationToken);
        return (await _connection.QueryAsync<User>(cmd).ConfigureAwait(false)).SingleOrDefault()!;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "DELETE FROM Users WHERE Id = @Id";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = "SELECT Id, Username, CreatedAt, UpdatedAt FROM Users";
        var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<User>(cmd).ConfigureAwait(false);
        return results.AsList();
    }
}
