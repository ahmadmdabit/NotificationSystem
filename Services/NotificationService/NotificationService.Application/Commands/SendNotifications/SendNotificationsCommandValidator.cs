namespace NotificationService.Application.Commands.SendNotifications;

using FluentValidation;

public sealed class SendNotificationsCommandValidator : AbstractValidator<SendNotificationsCommand>
{
    public SendNotificationsCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one recipient item is required.")
            .Must(items => items is not null && items.Count() <= 1000)
            .WithMessage("Batch may not exceed 1000 recipient items.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.NotificationId).GreaterThan(0);
            item.RuleFor(x => x.UserId).GreaterThan(0);
        });
    }
}
