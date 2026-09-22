using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Abstractions;

/// <summary>
/// Domain-defined repository contract for Notification aggregate.
/// </summary>
public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);
    Task<Notification> InsertAsync(Notification entity, CancellationToken cancellationToken = default);
    Task<Notification> UpdateAsync(Notification entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set-based Draft→Sent status flip for many ids in a single round trip (M-4).
    /// Only rows still <c>Draft</c> and not soft-deleted are updated (idempotent under
    /// concurrent sends); returns the number of rows affected.
    /// </summary>
    Task<int> MarkSentBatchAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
}
