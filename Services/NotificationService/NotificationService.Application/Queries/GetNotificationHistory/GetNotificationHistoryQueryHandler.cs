using MediatR;

using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;
using NotificationService.Domain.Abstractions;

namespace NotificationService.Application.Queries.GetNotificationHistory;

/// <summary>
/// Handles GetNotificationHistoryQuery.
/// </summary>
public sealed class GetNotificationHistoryQueryHandler : IRequestHandler<GetNotificationHistoryQuery, IReadOnlyList<NotificationHistoryDto>>
{
    private readonly INotificationHistoryRepository repository;

    public GetNotificationHistoryQueryHandler(INotificationHistoryRepository repository)
    {
        this.repository = repository;
    }

    public async Task<IReadOnlyList<NotificationHistoryDto>> Handle(GetNotificationHistoryQuery request, CancellationToken cancellationToken)
    {
        var records = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return records.Select(r => r.ToDto()).ToList();
    }
}
