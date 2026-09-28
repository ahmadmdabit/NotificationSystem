using System.Data;
using System.Data.Common;

using Dapper;

using Shared.Infrastructure.Persistence;

using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Builds the Dapper <see cref="CommandDefinition"/>s issued by <see cref="Repositories.UserRepository"/>.
/// </summary>
/// <remarks>
/// The SQL contract lives here, separate from command execution, so it is directly testable:
/// Dapper's <c>QuerySingleOrDefaultAsync</c>/<c>ExecuteAsync</c> are <b>static extension methods</b>
/// on <see cref="IDbConnection"/> rather than interface members, so a test double on the connection
/// cannot intercept them, and the generated <see cref="DbCommand"/> mock exposes neither
/// <c>CreateParameter()</c> nor <c>Parameters</c> (both are protected). These builders are pure —
/// they build a command and return it without executing anything.
/// <para>
/// The projection and soft-delete shapes are shared across services and are delegated to
/// <see cref="SqlCommands"/>. The stored-procedure invocations stay here because their
/// parameter shapes are specific to the Users domain.
/// </para>
/// </remarks>
public static class UserCommandFactory
{
    /// <summary>Columns projected by the read queries, in hydration order.</summary>
    public static readonly IReadOnlyList<string> UserColumns =
        ["Id", "Username", "PasswordHash", "PasswordSalt", "CreatedAt", "UpdatedAt"];

    private static readonly IReadOnlyList<string> IdKey = ["Id"];

    public static CommandDefinition GetById(long id, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectByKey("Users", UserColumns, new { Id = id }, IdKey, transaction, cancellationToken);

    /// <summary>
    /// Wired to the deployed <c>SPAuthenticateUser</c> — the SP that returns credential columns.
    /// <c>@Username</c> is the only parameter; the SP enforces <c>IsDeleted = 0</c>.
    /// </summary>
    public static CommandDefinition GetByUsername(string username, IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Username", username, DbType.String, ParameterDirection.Input, size: 50);

        return new CommandDefinition(
            "SPAuthenticateUser",
            parameters,
            transaction: transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Calls <c>SPRegisterUser</c>. Explicit names must match the SP's
    /// (<c>@Username</c>, <c>@PasswordHash</c>, <c>@PasswordSalt</c>); an anonymous projection would
    /// emit <c>@Hash</c>/<c>@Salt</c> and fail with SqlException 8144.
    /// </summary>
    public static CommandDefinition Insert(User entity, IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Username", entity.Username, DbType.String, ParameterDirection.Input, size: 50);
        parameters.Add("@PasswordHash", entity.Password.Hash, DbType.Binary, ParameterDirection.Input, size: 64);
        parameters.Add("@PasswordSalt", entity.Password.Salt, DbType.Binary, ParameterDirection.Input, size: 32);

        return new CommandDefinition(
            "SPRegisterUser",
            parameters,
            transaction: transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);
    }

    /// <summary>Soft delete — the row is flagged, never removed.</summary>
    public static CommandDefinition Delete(long id, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SoftDeleteByKey("Users", new { Id = id }, IdKey, transaction, cancellationToken);

    public static CommandDefinition GetAll(IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectAll("Users", UserColumns, transaction, cancellationToken);
}