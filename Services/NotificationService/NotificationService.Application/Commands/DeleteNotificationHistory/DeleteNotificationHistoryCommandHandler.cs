using MediatR;

namespace NotificationService.Application.Commands.DeleteNotificationHistory;

public sealed class DeleteNotificationHistoryCommandHandler : IRequestHandler<DeleteNotificationHistoryCommand, bool>
{
    private readonly Domain.Abstractions.INotificationHistoryRepository _repository;

    public DeleteNotificationHistoryCommandHandler(Domain.Abstractions.INotificationHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteNotificationHistoryCommand request, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(request.NotificationId, request.UserId, cancellationToken).ConfigureAwait(false);
    }
}
