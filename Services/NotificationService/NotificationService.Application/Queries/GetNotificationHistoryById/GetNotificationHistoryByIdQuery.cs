using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetNotificationHistoryById;

public sealed class GetNotificationHistoryByIdQuery : IRequest<NotificationHistoryDto?>
{
    public long NotificationId { get; }
    public long UserId { get; }

    public GetNotificationHistoryByIdQuery(long notificationId, long userId)
    {
        NotificationId = notificationId;
        UserId = userId;
    }
}
