using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Shared.Application.Abstractions;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based Notification repository implementing INotificationRepository.
/// Write operations use stored procedures; read operations use Dapper QueryAsync.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnection _connection;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt FROM Notifications WHERE Id = @Id AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { Id = id }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<Notification>(cmd).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Notification>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return [];

        var sql = "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt FROM Notifications WHERE Id IN @Ids AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { Ids = idList }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<Notification>(cmd).ConfigureAwait(false);
        return results.AsList();
    }

    public async Task<Notification> InsertAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            entity.Title,
            entity.Content,
            Status = (int)entity.Status,
            entity.CreatedAt
        };

        var cmd = new CommandDefinition(
            "sp_InsertNotification",
            parameters,
            transaction: _unitOfWork.Transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var result = await _connection.QuerySingleOrDefaultAsync<Notification>(cmd).ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Failed to insert notification.");
    }

    public async Task<Notification> UpdateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            entity.Id,
            entity.Title,
            entity.Content,
            Status = (int)entity.Status,
            entity.SentAt,
            entity.UpdatedAt
        };

        var cmd = new CommandDefinition(
            "sp_UpdateNotification",
            parameters,
            transaction: _unitOfWork.Transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        var affected = await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
        if (affected == 0)
            throw new Shared.Domain.Exceptions.NotFoundException("Notification", entity.Id.ToString());

        return entity;
    }

    public async Task<int> MarkSentBatchAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return 0;

        // Set-based (M-4): one round trip for the whole batch instead of one
        // sp_UpdateNotification call per notification. Guards mirror the SP contract:
        // IsDeleted = 0 plus Status = Draft, so a concurrent transition is never
        // overwritten and re-sends stay idempotent.
        const string sql = "UPDATE Notifications SET Status = @SentStatus, SentAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id IN @Ids AND IsDeleted = 0 AND Status = @DraftStatus";
        var cmd = new CommandDefinition(
            sql,
            new
            {
                Ids = ids.ToList(),
                SentStatus = (int)NotificationStatus.Sent,
                DraftStatus = (int)NotificationStatus.Draft
            },
            transaction: _unitOfWork.Transaction,
            cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "UPDATE Notifications SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0";
        var cmd = new CommandDefinition(sql, new { Id = id }, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt FROM Notifications WHERE IsDeleted = 0";
        var cmd = new CommandDefinition(sql, transaction: _unitOfWork.Transaction, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<Notification>(cmd).ConfigureAwait(false);
        return results.AsList();
    }
}