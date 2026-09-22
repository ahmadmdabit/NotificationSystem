using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based Notification repository implementing INotificationRepository.
/// Write operations use stored procedures; read operations use Dapper QueryAsync.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnection _connection;

    public NotificationRepository(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt FROM Notifications WHERE Id = @Id";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await _connection.QuerySingleOrDefaultAsync<Notification>(cmd).ConfigureAwait(false);
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

        var result = await _connection.QuerySingleOrDefaultAsync<Notification>(
            "sp_InsertNotification",
            parameters,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

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

        await _connection.ExecuteAsync(
            "sp_UpdateNotification",
            parameters,
            commandType: CommandType.StoredProcedure).ConfigureAwait(false);

        return entity;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var sql = "DELETE FROM Notifications WHERE Id = @Id";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        return await _connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = "SELECT Id, Title, Content, Status, SentAt, CreatedAt, UpdatedAt FROM Notifications";
        var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var results = await _connection.QueryAsync<Notification>(cmd).ConfigureAwait(false);
        return results.AsList();
    }
}
