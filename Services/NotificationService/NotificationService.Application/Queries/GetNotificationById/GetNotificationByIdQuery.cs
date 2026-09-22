using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetNotificationById;

/// <summary>
/// Query to get a notification by ID.
/// </summary>
public sealed class GetNotificationByIdQuery : IRequest<NotificationDto?>
{
    public long Id { get; }

    public GetNotificationByIdQuery(long id)
    {
        Id = id;
    }
}
