using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.Application.Abstractions;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based User repository implementing IUserRepository.
/// Write operations use stored procedures; read operations use Dapper QueryAsync.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private const string UserColumns = "Id, Username, PasswordHash, PasswordSalt, CreatedAt, UpdatedAt";

    private readonly IDbConnection _connection;
    private readonly IUnitOfWork _unitOfWork;

    public UserRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {UserColumns} FROM Users WHERE Id = @Id AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { Id = id }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var row = await _connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);
        return row is null ? null : ToEntity(row);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        // Wired to the deployed sp_AuthenticateUser (root-cause cluster #1: the SP that
        // returns credential columns was previously orphaned — L-4). Same projection as
        // UserColumns; @Username is the only parameter and the SP enforces IsDeleted = 0.
        var parameters = new DynamicParameters();
        parameters.Add("@Username", username, DbType.String, ParameterDirection.Input, size: 50);

        var cmd = new CommandDefinition(
            "sp_AuthenticateUser",
            parameters,
            transaction: _unitOfWork.Transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var row = await _connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);
        return row is null ? null : ToEntity(row);
    }

    public async Task<User> InsertAsync(User entity, CancellationToken cancellationToken = default)
    {
        // Explicit names must match sp_RegisterUser (@Username, @PasswordHash, @PasswordSalt);
        // an anonymous projection would emit @Hash/@Salt and fail with SqlException 8144.
        var parameters = new DynamicParameters();
        parameters.Add("@Username", entity.Username, DbType.String, ParameterDirection.Input, size: 50);
        parameters.Add("@PasswordHash", entity.Password.Hash, DbType.Binary, ParameterDirection.Input, size: 64);
        parameters.Add("@PasswordSalt", entity.Password.Salt, DbType.Binary, ParameterDirection.Input, size: 32);

        var cmd = new CommandDefinition(
            "sp_RegisterUser",
            parameters,
            transaction: _unitOfWork.Transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var row = await _connection.QuerySingleOrDefaultAsync<UserRow>(cmd).ConfigureAwait(false);

        return row is null
            ? throw new InvalidOperationException("Failed to insert user.")
            : ToEntity(row);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "UPDATE Users SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { Id = id }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {UserColumns} FROM Users WHERE IsDeleted = 0";
        var cmd = new CommandDefinition(sql, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var rows = await _connection.QueryAsync<UserRow>(cmd).ConfigureAwait(false);
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