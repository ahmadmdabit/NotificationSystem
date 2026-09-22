using Shared.Application.Behaviors;

namespace NotificationService.Application.Commands.CreateNotification;

/// <summary>
/// Single validation source: FluentValidation (CreateNotificationCommandValidator).
/// </summary>
public sealed class CreateNotificationCommand : MediatR.IRequest<DTOs.NotificationDto>, ICommand
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
