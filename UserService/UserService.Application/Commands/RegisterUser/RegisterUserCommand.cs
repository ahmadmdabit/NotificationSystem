using System.ComponentModel.DataAnnotations;
using UserService.Application.Behaviors;

namespace UserService.Application.Commands.RegisterUser;

/// <summary>
/// Command to register a new user.
/// </summary>
public sealed class RegisterUserCommand : MediatR.IRequest<UserService.Application.DTOs.UserDto>, ICommand
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
