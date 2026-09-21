using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;
using NotificationService.Domain;
using NotificationService.Domain.Abstractions;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Events;

namespace NotificationService.Application.Commands.SendNotifications;

/// <summary>
/// Handles SendNotificationsCommand.
/// </summary>
public sealed class SendNotificationsCommandHandler : IRequestHandler<SendNotificationsCommand, bool>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationHistoryRepository _historyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public SendNotificationsCommandHandler(
        INotificationRepository notificationRepository,
        INotificationHistoryRepository historyRepository,
        IUnitOfWork unitOfWork,
        IDomainEventDispatcher eventDispatcher)
    {
        _notificationRepository = notificationRepository;
        _historyRepository = historyRepository;
        _unitOfWork = unitOfWork;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> Handle(SendNotificationsCommand request, CancellationToken cancellationToken)
    {
        // Create notification entity
        var notification = Notification.Create(request.Title, request.Content);

        // Persist notification
        var created = await _notificationRepository.InsertAsync(notification, cancellationToken).ConfigureAwait(false);

        // Create history records for each recipient
        var historyRecords = request.RecipientIds.Select(userId => new NotificationHistory
        {
            NotificationId = created.Id,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        foreach (var record in historyRecords)
        {
            await _historyRepository.InsertAsync(record, cancellationToken).ConfigureAwait(false);
        }

        // Mark as sent
        created.MarkAsSent();
        await _notificationRepository.UpdateAsync(created, cancellationToken).ConfigureAwait(false);

        // Dispatch domain event
        var evt = new NotificationSentEvent(created.Id, request.RecipientIds);
        await _eventDispatcher.PublishAsync(evt, cancellationToken).ConfigureAwait(false);

        return true;
    }
}
