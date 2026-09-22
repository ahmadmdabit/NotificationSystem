using MediatR;

namespace NotificationService.Application.Commands.DeleteNotification;

public sealed class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand, bool>
{
    private readonly Domain.Abstractions.INotificationRepository _repository;

    public DeleteNotificationCommandHandler(Domain.Abstractions.INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
    }
}
