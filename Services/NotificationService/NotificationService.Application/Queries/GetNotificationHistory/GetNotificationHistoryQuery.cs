using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries.GetNotificationHistory;

/// <summary>
/// Query to get all notification history records.
/// </summary>
public sealed class GetNotificationHistoryQuery : IRequest<IReadOnlyList<NotificationHistoryDto>>
{
}
