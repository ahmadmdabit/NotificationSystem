using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Shared.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Infrastructure.Services;

namespace UserService.Tests.Infrastructure;

/// <summary>
/// Verifies the token contract of <see cref="JwtTokenService"/>.
/// </summary>
/// <remarks>
/// <para>Claims are asserted by <b>type</b>, never by value. The service emits
/// <see cref="ClaimTypes.Name"/> and <see cref="ClaimTypes.Role"/>, which
/// <c>JwtSecurityTokenHandler</c> shortens to <c>"name"</c> / <c>"role"</c> on read. A previous
/// version of these tests matched on <c>c.Value</c>, which any unrelated claim carrying the same
/// value would satisfy.</para>
/// <para>The key is taken as raw ASCII bytes — <c>JwtTokenService</c> calls
/// <c>Encoding.ASCII.GetBytes(Secret)</c> and does <b>not</b> hex-decode. Whether the documented
/// secret-generation command carries enough entropy is tracked separately as a production
/// decision (see <c>.hermes/todos/01.md</c> T18), not asserted here.</para>
/// </remarks>
public class JwtTokenServiceTests
{
    /// <summary>
    /// 64 characters — the shape AGENTS.md's <c>openssl rand -hex 64</c> produces. The service
    /// takes the raw ASCII bytes (it does not hex-decode), so this yields the same 64-byte key the
    /// documented command does, exercising the production key shape.
    /// </summary>
    private const string Secret = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const long ExpiryDays = 7;

    private static JwtTokenService CreateService(string? secret = Secret)
        => new(Options.Create(new AppSettings
        {
            Secret = secret!,
            ServiceAccountUsername = "uiservice"
        }));

    /// <summary>Reads the token and maps claims the same way the auth pipeline does.</summary>
    private static ClaimsPrincipal Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(
            token,
            new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Secret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false
            },
            out _);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Constructor_WhenSecretIsMissingOrWhitespace_ThrowsInvalidOperationException(string? secret)
    {
        // Act & Assert — the constructor uses IsNullOrWhiteSpace, so all three must be rejected.
        await Assert.That(() => CreateService(secret))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessage("AppSettings:Secret is not configured.");
    }

    [Test]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new JwtTokenService(null!));

    [Test]
    public async Task GenerateToken_EmitsUserIdAsTheNameClaim()
    {
        // Act
        var token = CreateService().GenerateToken(42);

        // Assert — claim type, not value.
        var principal = Validate(token);
        var nameClaim = principal.FindFirst(ClaimTypes.Name);
        await Assert.That(nameClaim).IsNotNull();
        await Assert.That(nameClaim.Value).IsEqualTo("42");
    }

    [Test]
    public async Task GenerateToken_WithoutRole_EmitsNoRoleClaim()
    {
        // Act
        var token = CreateService().GenerateToken(42);

        // Assert
        var principal = Validate(token);
        await Assert.That(principal.FindAll(ClaimTypes.Role)).IsEmpty();
        await Assert.That(principal.IsInRole("admin")).IsFalse();
    }

    [Test]
    public async Task GenerateToken_WithRole_AddsRoleClaim()
    {
        // Act
        var token = CreateService().GenerateToken(99, "admin");

        // Assert
        var principal = Validate(token);
        var role = principal.FindFirst(ClaimTypes.Role);
        await Assert.That(role).IsNotNull();
        await Assert.That(role.Value).IsEqualTo("admin");
        await Assert.That(principal.IsInRole("admin")).IsTrue();
        await Assert.That(principal.FindFirst(ClaimTypes.Name)!.Value).IsEqualTo("99");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task GenerateToken_WithBlankRole_EmitsNoRoleClaim(string blankRole)
    {
        // Act — a blank role must be treated as absent, not emitted as an empty claim.
        var token = CreateService().GenerateToken(7, blankRole);

        // Assert
        var principal = Validate(token);
        await Assert.That(principal.FindAll(ClaimTypes.Role)).IsEmpty();
    }

    [Test]
    public async Task GenerateToken_ExpiresInSevenDays()
    {
        // Act
        var before = DateTime.UtcNow;
        var token = CreateService().GenerateToken(1);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        await Assert.That(jwt.ValidTo).IsGreaterThan(before.AddDays(ExpiryDays - 1));
        await Assert.That(jwt.ValidTo).IsLessThanOrEqualTo(before.AddDays(ExpiryDays + 1));
    }

    [Test]
    public async Task GenerateToken_IsSignedWithTheConfiguredSecret()
    {
        // Act
        var token = CreateService().GenerateToken(1);

        // Assert — a token signed with a different key must not validate.
        var handler = new JwtSecurityTokenHandler();
        await Assert.That(() => handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes("a-different-secret-value-32chr")),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                },
                out _))
            .Throws<SecurityTokenException>();
    }
}
