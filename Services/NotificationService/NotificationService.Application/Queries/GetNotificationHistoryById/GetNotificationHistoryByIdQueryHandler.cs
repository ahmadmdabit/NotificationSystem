using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;

namespace NotificationService.Application.Queries.GetNotificationHistoryById;

public sealed class GetNotificationHistoryByIdQueryHandler : IRequestHandler<GetNotificationHistoryByIdQuery, NotificationHistoryDto?>
{
    private readonly Domain.Abstractions.INotificationHistoryRepository _repository;

    public GetNotificationHistoryByIdQueryHandler(Domain.Abstractions.INotificationHistoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationHistoryDto?> Handle(GetNotificationHistoryByIdQuery request, CancellationToken cancellationToken)
    {
        var record = await _repository.GetByIdAsync(request.NotificationId, request.UserId, cancellationToken).ConfigureAwait(false);
        return record?.ToDto();
    }
}
