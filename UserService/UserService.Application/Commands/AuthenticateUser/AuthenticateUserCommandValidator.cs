using FluentValidation;

namespace UserService.Application.Commands.AuthenticateUser;

/// <summary>
/// Validator for AuthenticateUserCommand.
/// </summary>
public sealed class AuthenticateUserCommandValidator : AbstractValidator<AuthenticateUserCommand>
{
    public AuthenticateUserCommandValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
