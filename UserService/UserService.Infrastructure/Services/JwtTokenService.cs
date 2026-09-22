using Common.Helpers;
using Microsoft.Extensions.Options;
using UserService.Domain.Abstractions;

namespace UserService.Infrastructure.Services;

/// <summary>
/// JWT token service implementing ITokenService (HMAC-SHA256, 7-day expiry).
/// Uses IOptions<AppSettings> for configuration.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly AppSettings _settings;

    public JwtTokenService(IOptions<AppSettings> options)
    {
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_settings.Secret))
            throw new InvalidOperationException("AppSettings:Secret is not configured.");
    }

    public string GenerateToken(long userId)
    {
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes(_settings.Secret);
        var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Subject = new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userId.ToString()) }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
