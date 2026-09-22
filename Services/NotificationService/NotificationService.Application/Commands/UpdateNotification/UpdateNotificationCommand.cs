using System.ComponentModel.DataAnnotations;
using Shared.Application.Behaviors;

namespace NotificationService.Application.Commands.UpdateNotification;

public sealed class UpdateNotificationCommand : MediatR.IRequest<DTOs.NotificationDto?>, ICommand
{
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}