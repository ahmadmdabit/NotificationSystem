namespace NotificationService.Domain.Abstractions;

/// <summary>
/// Domain-defined repository contract for NotificationHistory.
/// </summary>
public interface INotificationHistoryRepository
{
    Task<NotificationHistory?> GetByIdAsync(long notificationId, long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationHistory>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<NotificationHistory> InsertAsync(NotificationHistory entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long notificationId, long userId, CancellationToken cancellationToken = default);
}
