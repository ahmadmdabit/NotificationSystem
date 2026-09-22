using Shared.Application.Behaviors;

namespace NotificationService.Application.Commands.DeleteNotification;

public sealed class DeleteNotificationCommand : MediatR.IRequest<bool>, ICommand
{
    public long Id { get; }

    public DeleteNotificationCommand(long id)
    {
        Id = id;
    }
}
