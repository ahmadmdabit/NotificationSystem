using MediatR;
using NotificationService.Application.DTOs;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Commands.CreateNotification;

public sealed class CreateNotificationCommandHandler : IRequestHandler<CreateNotificationCommand, NotificationDto>
{
    private readonly Domain.Abstractions.INotificationRepository _repository;

    public CreateNotificationCommandHandler(Domain.Abstractions.INotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationDto> Handle(CreateNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = Notification.Create(request.Title, request.Content);
        var created = await _repository.InsertAsync(notification, cancellationToken).ConfigureAwait(false);

        return new NotificationDto
        {
            Id = created.Id,
            Title = created.Title,
            Content = created.Content,
            Status = created.Status.ToString(),
            CreatedAt = created.CreatedAt
        };
    }
}
