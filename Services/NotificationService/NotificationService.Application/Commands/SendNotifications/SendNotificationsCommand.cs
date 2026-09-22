using System.ComponentModel.DataAnnotations;
using Shared.Application.Behaviors;

namespace NotificationService.Application.Commands.SendNotifications;

public sealed class SendNotificationItem
{
    [Required]
    public long NotificationId { get; set; }

    [Required]
    public long UserId { get; set; }
}

/// <summary>
/// Command to send notifications to users (matches UI contract: list of history records).
/// </summary>
public sealed class SendNotificationsCommand : MediatR.IRequest<bool>, ICommand
{
    /// <summary>
    /// Batch cap enforced by FluentValidation (SendNotificationsCommandValidator);
    /// [ApiController] model-state validation is suppressed so the ApiResult envelope holds.
    /// </summary>
    public IEnumerable<SendNotificationItem> Items { get; set; } = [];
}