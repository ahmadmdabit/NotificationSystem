using MediatR;

namespace NotificationService.Application.Commands.DeleteNotification;

public sealed class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand, bool>
{
    private readonly Domain.Abstractions.INotificationRepository repository;

    public DeleteNotificationCommandHandler(Domain.Abstractions.INotificationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<bool> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        return await repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }
}
