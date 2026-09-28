using MediatR;

using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetAllNotifications;

public sealed class GetAllNotificationsQuery : IRequest<IReadOnlyList<NotificationDto>>
{
}
