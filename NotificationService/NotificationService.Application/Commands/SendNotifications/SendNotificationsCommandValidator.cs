using FluentValidation;

namespace NotificationService.Application.Commands.SendNotifications;

/// <summary>
/// Validator for SendNotificationsCommand.
/// </summary>
public sealed class SendNotificationsCommandValidator : AbstractValidator<SendNotificationsCommand>
{
    public SendNotificationsCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required.")
            .MaximumLength(4000).WithMessage("Content must not exceed 4000 characters.");

        RuleFor(x => x.RecipientIds)
            .NotEmpty().WithMessage("At least one recipient is required.");
    }
}
