using Shared.Application.Behaviors;

namespace NotificationService.Application.Commands.DeleteNotificationHistory;

public sealed class DeleteNotificationHistoryCommand : MediatR.IRequest<bool>, ICommand
{
    public long NotificationId { get; }
    public long UserId { get; }

    public DeleteNotificationHistoryCommand(long notificationId, long userId)
    {
        NotificationId = notificationId;
        UserId = userId;
    }
}
