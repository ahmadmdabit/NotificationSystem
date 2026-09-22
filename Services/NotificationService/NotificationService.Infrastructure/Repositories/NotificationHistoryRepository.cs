using System.Data;
using Dapper;
using NotificationService.Domain;
using Shared.Application.Abstractions;
using NotificationService.Domain.Abstractions;
using NotificationService.Infrastructure.Data.Tvp;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based NotificationHistory repository implementing INotificationHistoryRepository.
/// Bulk writes stream via TVP (SPNotificationHistoryInsert); SP output params are surfaced as exceptions.
/// </summary>
public sealed class NotificationHistoryRepository : INotificationHistoryRepository
{
    private readonly IDbConnection _connection;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationHistoryRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<NotificationHistory?> GetByIdAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT NotificationId, UserId, CreatedAt, UpdatedAt FROM NotificationHistories WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { NotificationId = notificationId, UserId = userId }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<NotificationHistory>(cmd).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT NotificationId, UserId, CreatedAt, UpdatedAt FROM NotificationHistories WHERE IsDeleted = 0";
        var cmd = new CommandDefinition(sql, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<NotificationHistory>(cmd).ConfigureAwait(false);
        return results.AsList();
    }

    public async Task<NotificationHistory> InsertAsync(NotificationHistory entity, CancellationToken cancellationToken = default)
    {
        await InsertCoreAsync(new[] { entity }, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public Task<IReadOnlyList<NotificationHistory>> InsertBulkAsync(IReadOnlyList<NotificationHistory> entities, CancellationToken cancellationToken = default)
    {
        return InsertCoreAsync(entities, cancellationToken);
    }

    private async Task<IReadOnlyList<NotificationHistory>> InsertCoreAsync(IReadOnlyList<NotificationHistory> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
            return entities;

        var tvpDefinition = NotificationHistoryTvpDefinition.Instance;
        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Entities",
            value: entities.AsSqlDataRecords(tvpDefinition).AsTableValuedParameter(tvpDefinition.TypeName)
        );
        parameters.Add("@SPSuccess", value: null, dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@SPMessage", value: null, dbType: DbType.String, direction: ParameterDirection.Output, size: 255);

        var cmd = new CommandDefinition(
            "[dbo].[SPNotificationHistoryInsert]",
            parameters,
            transaction: _unitOfWork.Transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        await _connection.ExecuteAsync(cmd).ConfigureAwait(false);

        if (!parameters.Get<bool>("@SPSuccess"))
            throw new InvalidOperationException($"SPNotificationHistoryInsert failed: {parameters.Get<string>("@SPMessage")}");

        return entities;
    }

    public async Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        var sql = "UPDATE NotificationHistories SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { NotificationId = notificationId, UserId = userId }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }
}
