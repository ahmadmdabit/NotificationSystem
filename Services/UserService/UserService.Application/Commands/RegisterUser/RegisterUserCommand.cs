using Shared.Application.Behaviors;

namespace UserService.Application.Commands.RegisterUser;

/// <summary>
/// Command to register a new user.
/// Single validation source: FluentValidation (RegisterUserCommandValidator);
/// [ApiController] model-state validation is suppressed so the ApiResult envelope holds.
/// </summary>
public sealed class RegisterUserCommand : MediatR.IRequest<UserService.Application.DTOs.UserDto>, ICommand
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
