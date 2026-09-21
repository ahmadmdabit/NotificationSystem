using System.ComponentModel.DataAnnotations;
using MediatR;
using UserService.Application.DTOs;

namespace UserService.Application.Commands.AuthenticateUser;

/// <summary>
/// Command to authenticate a user.
/// </summary>
public sealed class AuthenticateUserCommand : IRequest<AuthenticateResultDto?>
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
