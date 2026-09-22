using MediatR;
using Microsoft.Extensions.Options;
using Shared.Helpers;
using UserService.Application.DTOs;
using UserService.Domain.Abstractions;
using UserService.Domain.Entities;

namespace UserService.Application.Commands.AuthenticateUser;

/// <summary>
/// Handles AuthenticateUserCommand. Mints the "service" role for the configured
/// service account (used by the UI BFF); all other users receive a roleless token.
/// </summary>
public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticateResultDto?>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AppSettings _settings;

    public AuthenticateUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AppSettings> settings)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<AuthenticateResultDto?> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Dummy PBKDF2 on credential-miss: equalizes CPU cost so unknown usernames do not
            // answer measurably faster than wrong-password attempts (timing oracle).
            _passwordHasher.HashPassword(request.Password, out _, out _);
            return null;
        }

        try
        {
            user.VerifyPassword(request.Password, _passwordHasher);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var role = IsServiceAccount(user.Username) ? "service" : null;
        var token = _tokenService.GenerateToken(user.Id, role);

        return new AuthenticateResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            Token = token
        };
    }

    private bool IsServiceAccount(string username)
        => string.Equals(username, _settings.ServiceAccountUsername, StringComparison.OrdinalIgnoreCase);
}
