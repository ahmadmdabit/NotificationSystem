using MediatR;
using UserService.Application.DTOs;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Application.Commands.AuthenticateUser;

/// <summary>
/// Handles AuthenticateUserCommand.
/// </summary>
public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticateResultDto?>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthenticateUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthenticateResultDto?> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return null;

        try
        {
            user.VerifyPassword(request.Password, _passwordHasher);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var token = _tokenService.GenerateToken(user.Id);

        return new AuthenticateResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            Token = token
        };
    }
}
