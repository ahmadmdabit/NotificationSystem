using MediatR;

using Microsoft.Extensions.Options;

using Shared.Helpers;

using UserService.Application.DTOs;
using UserService.Domain.Abstractions;

namespace UserService.Application.Commands.AuthenticateUser;

/// <summary>
/// Handles AuthenticateUserCommand. Mints the "service" role for the configured
/// service account (used by the UI BFF); all other users receive a roleless token.
/// </summary>
public sealed class AuthenticateUserCommandHandler : IRequestHandler<AuthenticateUserCommand, AuthenticateResultDto?>
{
    private readonly IUserRepository repository;
    private readonly IPasswordHasher passwordHasher;
    private readonly ITokenService tokenService;
    private readonly AppSettings settings;

    public AuthenticateUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AppSettings> settings)
    {
        this.repository = repository;
        this.passwordHasher = passwordHasher;
        this.tokenService = tokenService;
        this.settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<AuthenticateResultDto?> Handle(AuthenticateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByUsernameAsync(request.Username, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            // Dummy PBKDF2 on credential-miss: equalizes CPU cost so unknown usernames do not
            // answer measurably faster than wrong-password attempts (timing oracle).
            passwordHasher.HashPassword(request.Password, out _, out _);
            return null;
        }

        try
        {
            user.VerifyPassword(request.Password, passwordHasher);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var role = IsServiceAccount(user.Username) ? "service" : null;
        var token = tokenService.GenerateToken(user.Id, role);

        return new AuthenticateResultDto
        {
            UserId = user.Id,
            Username = user.Username,
            Token = token
        };
    }

    private bool IsServiceAccount(string username)
        => string.Equals(username, settings.ServiceAccountUsername, StringComparison.OrdinalIgnoreCase);
}
