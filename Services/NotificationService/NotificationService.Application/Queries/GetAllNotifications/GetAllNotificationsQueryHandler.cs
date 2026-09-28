using MediatR;

using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;

namespace NotificationService.Application.Queries.GetAllNotifications;

public sealed class GetAllNotificationsQueryHandler : IRequestHandler<GetAllNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    private readonly Domain.Abstractions.INotificationRepository repository;

    public GetAllNotificationsQueryHandler(Domain.Abstractions.INotificationRepository repository)
    {
        this.repository = repository;
    }

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetAllNotificationsQuery request, CancellationToken cancellationToken)
    {
        var notifications = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return notifications.Select(n => n.ToDto()).ToList();
    }
}
