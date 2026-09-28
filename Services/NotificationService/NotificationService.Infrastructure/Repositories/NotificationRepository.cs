using System.Data;

using Dapper;

using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;
using NotificationService.Infrastructure.Persistence;

using Shared.Application.Abstractions;
using Shared.Domain.Exceptions;

namespace NotificationService.Infrastructure.Repositories;

/// <summary>
/// Dapper-based Notification repository implementing INotificationRepository.
/// </summary>
/// <remarks>
/// The SQL contract is built by <see cref="NotificationCommandFactory"/> so it can be verified
/// directly; this type only executes. Dapper's query methods are static extension methods on
/// <see cref="System.Data.IDbConnection"/> and cannot be intercepted by a test double, so the
/// commands are the observable unit here.
/// </remarks>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnection connection;
    private readonly IUnitOfWork unitOfWork;

    public NotificationRepository(IDbConnection connection, IUnitOfWork unitOfWork)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.GetNotificationById(id, unitOfWork.Transaction, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<NotificationRow>(cmd).ConfigureAwait(false);
        return row is null ? null : ToEntity(row);
    }

    public async Task<IReadOnlyList<Notification>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return [];

        var cmd = NotificationCommandFactory.GetNotificationsByIds(idList, unitOfWork.Transaction, cancellationToken);
        var rows = await connection.QueryAsync<NotificationRow>(cmd).ConfigureAwait(false);
        return rows.Select(ToEntity).ToList();
    }

    public async Task<Notification> InsertAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.InsertNotification(entity, unitOfWork.Transaction, cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<NotificationRow>(cmd).ConfigureAwait(false);

        return row is null
            ? throw new InvalidOperationException("Failed to insert notification.")
            : ToEntity(row);
    }

    public async Task<Notification> UpdateAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.UpdateNotification(entity, unitOfWork.Transaction, cancellationToken);

        var affected = await connection.ExecuteAsync(cmd).ConfigureAwait(false);
        if (affected == 0)
            throw new NotFoundException("Notification", entity.Id.ToString());

        return entity;
    }

    public async Task<int> MarkSentBatchAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return 0;

        var idList = ids.ToList();
        var cmd = NotificationCommandFactory.MarkSentBatch(idList, unitOfWork.Transaction, cancellationToken);
        return await connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.SoftDeleteNotification(id, unitOfWork.Transaction, cancellationToken);
        return await connection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cmd = NotificationCommandFactory.GetAllNotifications(unitOfWork.Transaction, cancellationToken);
        var rows = await connection.QueryAsync<NotificationRow>(cmd).ConfigureAwait(false);
        return rows.Select(ToEntity).ToList();
    }

    /// <summary>
    /// Hydration DTO mirroring the persisted column shape; never leaves the repository.
    /// </summary>
    /// <remarks>
    /// <b>Private on purpose.</b> A public row type would put the raw persistence shape on this
    /// assembly's public API surface.
    /// <para>
    /// <b>Every member name must match a column in
    /// <see cref="NotificationCommandFactory.NotificationColumns"/>.</b> Dapper maps by name and
    /// silently assigns <c>default</c> for anything it does not find, so a typo here produces a
    /// half-populated entity rather than an error. A test asserts the two name sets are identical.
    /// </para>
    /// <para>
    /// <b>Not covered by any test:</b> Dapper row mapping and the Dapper execute call both need a
    /// live ADO.NET provider. The observable unit in this assembly is the
    /// <see cref="Dapper.CommandDefinition"/>; only the command shapes are tested.
    /// </para>
    /// </remarks>
    private sealed class NotificationRow
    {
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public NotificationStatus Status { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    private static Notification ToEntity(NotificationRow row)
        => Notification.Rehydrate(
            row.Id,
            row.Title,
            row.Content,
            row.Status,
            row.SentAt,
            row.CreatedAt,
            row.UpdatedAt);
}