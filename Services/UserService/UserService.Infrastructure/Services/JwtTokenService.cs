using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shared.Helpers;

using UserService.Domain.Abstractions;

namespace UserService.Infrastructure.Services;

/// <summary>
/// JWT token service implementing ITokenService (HMAC-SHA256, 7-day expiry).
/// Uses IOptions<AppSettings> for configuration.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly AppSettings settings;

    public JwtTokenService(IOptions<AppSettings> options)
    {
        settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(settings.Secret))
            throw new InvalidOperationException("AppSettings:Secret is not configured.");
    }

    public string GenerateToken(long userId, string? role = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(settings.Secret);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}