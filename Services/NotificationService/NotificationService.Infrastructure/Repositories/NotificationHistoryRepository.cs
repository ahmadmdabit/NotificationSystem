using System.Data;

using Dapper;

using NotificationService.Domain;
using NotificationService.Domain.Abstractions;
using NotificationService.Infrastructure.Data.Tvp;
using NotificationService.Infrastructure.Persistence;

using Shared.Application.Abstractions;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based NotificationHistory repository implementing INotificationHistoryRepository.
/// Bulk writes stream via TVP (SPNotificationHistoryInsert); SP output params are surfaced as exceptions.
/// </summary>
/// <remarks>
/// The SQL contract is built by <see cref="NotificationCommandFactory"/>; this type only executes.
/// </remarks>
public sealed class NotificationHistoryRepository : INotificationHistoryRepository
{
    private readonly IDbConnection connection;
    private readonly IUnitOfWork unitOfWork;

    public NotificationHistoryRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<NotificationHistory?> GetByIdAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.GetHistoryById(notificationId, userId, unitOfWork.Transaction, cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<NotificationHistory>(cmd).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationHistory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.GetAllHistory(unitOfWork.Transaction, cancellationToken);
        var results = await connection.QueryAsync<NotificationHistory>(cmd).ConfigureAwait(false);
        return results.AsList();
    }

    public async Task<NotificationHistory> InsertAsync(NotificationHistory entity, CancellationToken cancellationToken = default)
    {
        await InsertCoreAsync([entity], cancellationToken).ConfigureAwait(false);
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
        var parameters = NotificationCommandFactory.CreateHistoryBulkParameters(entities, tvpDefinition);
        var cmd = NotificationCommandFactory.HistoryBulkInsert(parameters, unitOfWork.Transaction, cancellationToken);

        await connection.ExecuteAsync(cmd).ConfigureAwait(false);

        if (!parameters.Get<bool>("@SPSuccess"))
            throw new InvalidOperationException($"SPNotificationHistoryInsert failed: {parameters.Get<string>("@SPMessage")}");

        return entities;
    }

    public async Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.SoftDeleteHistory(notificationId, userId, unitOfWork.Transaction, cancellationToken);
        return await connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }
}