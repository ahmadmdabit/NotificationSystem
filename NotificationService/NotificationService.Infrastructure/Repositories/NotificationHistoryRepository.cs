using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NotificationService.Domain;
using NotificationService.Domain.Abstractions;
using NotificationService.Infrastructure.Data.Tvp;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based NotificationHistory repository implementing INotificationHistoryRepository.
/// Uses the existing SPNotificationHistoryInsert stored procedure for bulk TVP insert.
/// </summary>
public sealed class NotificationHistoryRepository : INotificationHistoryRepository
{
    private readonly IDbConnection _connection;

    public NotificationHistoryRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<NotificationHistory?> GetByIdAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT NotificationId, UserId, CreatedAt, UpdatedAt FROM NotificationHistories WHERE NotificationId = @NotificationId AND UserId = @UserId";
        var cmd = new CommandDefinition(sql, new { NotificationId = notificationId, UserId = userId }, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<NotificationHistory>(cmd).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = "SELECT NotificationId, UserId, CreatedAt, UpdatedAt FROM NotificationHistories";
        var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<NotificationHistory>(cmd).ConfigureAwait(false);
        return results.AsList();
    }

    public async Task<NotificationHistory> InsertAsync(NotificationHistory entity, CancellationToken cancellationToken = default)
    {
        var tvpDefinition = NotificationHistoryTvpDefinition.Instance;
        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Entities",
            value: new[] { entity }.AsSqlDataRecords(tvpDefinition)
                           .AsTableValuedParameter(tvpDefinition.TypeName)
        );
        parameters.Add("@SPSuccess", value: null, dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@SPMessage", value: null, dbType: DbType.String, direction: ParameterDirection.Output, size: 255);

        await _connection.ExecuteAsync(
            "[dbo].[SPNotificationHistoryInsert]",
            parameters,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return entity;
    }

    public async Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        var sql = "DELETE FROM NotificationHistories WHERE NotificationId = @NotificationId AND UserId = @UserId";
        var cmd = new CommandDefinition(sql, new { NotificationId = notificationId, UserId = userId }, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }
}
