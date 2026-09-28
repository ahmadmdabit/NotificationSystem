using MediatR;

using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;
using NotificationService.Domain.Abstractions;

namespace NotificationService.Application.Queries.GetNotificationById;

/// <summary>
/// Handles GetNotificationByIdQuery.
/// </summary>
public sealed class GetNotificationByIdQueryHandler : IRequestHandler<GetNotificationByIdQuery, NotificationDto?>
{
    private readonly INotificationRepository repository;

    public GetNotificationByIdQueryHandler(INotificationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<NotificationDto?> Handle(GetNotificationByIdQuery request, CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (notification is null)
            return null;

        return notification.ToDto();
    }
}
