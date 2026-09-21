namespace NotificationService.Application.Interfaces;

/// <summary>
/// Application service contract for Notification operations.
/// </summary>
public interface INotificationService
{
    Task<bool> SendNotificationsAsync(string title, string content, IReadOnlyList<long> recipientIds, CancellationToken cancellationToken = default);
    Task<DTOs.NotificationDto?> GetNotificationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DTOs.NotificationHistoryDto>> GetNotificationHistoryAsync(CancellationToken cancellationToken = default);
}
