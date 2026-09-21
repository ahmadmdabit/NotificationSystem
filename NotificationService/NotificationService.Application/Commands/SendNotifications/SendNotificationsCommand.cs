using System.ComponentModel.DataAnnotations;
using NotificationService.Application.Behaviors;

namespace NotificationService.Application.Commands.SendNotifications;

/// <summary>
/// Command to send notifications to users.
/// </summary>
public sealed class SendNotificationsCommand : MediatR.IRequest<bool>, ICommand
{
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Required]
    public IReadOnlyList<long> RecipientIds { get; set; } = [];
}
