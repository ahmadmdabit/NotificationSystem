using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Application.Mappings;
using NotificationService.Domain.Abstractions;
using Shared.Domain.Exceptions;

namespace NotificationService.Application.Commands.UpdateNotification;

public sealed class UpdateNotificationCommandHandler : IRequestHandler<UpdateNotificationCommand, NotificationDto?>
{
    private readonly INotificationRepository _repository;

    public UpdateNotificationCommandHandler(INotificationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<NotificationDto?> Handle(UpdateNotificationCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return null;

        existing.Update(request.Title, request.Content);
        var updated = await _repository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

        return NotificationMapping.ToDto(updated);
    }
}