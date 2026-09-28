namespace UserService.Application.Commands.AuthenticateUser;

using MediatR;

using UserService.Application.DTOs;

/// <summary>
/// Command to authenticate a user.
/// Single validation source: FluentValidation (AuthenticateUserCommandValidator);
/// [ApiController] model-state validation is suppressed so the ApiResult envelope holds.
/// </summary>
public sealed class AuthenticateUserCommand : IRequest<AuthenticateResultDto?>
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
