using MediatR;

using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;
using NotificationService.Domain.Abstractions;

namespace NotificationService.Application.Commands.UpdateNotification;

public sealed class UpdateNotificationCommandHandler : IRequestHandler<UpdateNotificationCommand, NotificationDto?>
{
    private readonly INotificationRepository repository;

    public UpdateNotificationCommandHandler(INotificationRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<NotificationDto?> Handle(UpdateNotificationCommand request, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return null;

        existing.Update(request.Title, request.Content);
        var updated = await repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        return NotificationMapping.ToDto(updated);
    }
}