using NotificationService.Domain.Entities;

namespace NotificationService.Domain.Abstractions;

/// <summary>
/// Domain-defined repository contract for Notification aggregate.
/// </summary>
public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Notification> InsertAsync(Notification entity, CancellationToken cancellationToken = default);
    Task<Notification> UpdateAsync(Notification entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
}
