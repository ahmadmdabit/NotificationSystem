namespace NotificationService.Application.Mappings;

/// <summary>
/// Mapping methods for Notification domain entity to DTOs.
/// </summary>
public static class NotificationMapping
{
    public static DTOs.NotificationDto ToDto(this Domain.Entities.Notification notification)
    {
        return new DTOs.NotificationDto
        {
            Id = notification.Id,
            Title = notification.Title,
            Content = notification.Content,
            Status = notification.Status.ToString(),
            SentAt = notification.SentAt,
            CreatedAt = notification.CreatedAt
        };
    }

    public static DTOs.NotificationHistoryDto ToDto(this Domain.NotificationHistory history)
    {
        return new DTOs.NotificationHistoryDto
        {
            NotificationId = history.NotificationId,
            UserId = history.UserId,
            CreatedAt = history.CreatedAt
        };
    }
}
