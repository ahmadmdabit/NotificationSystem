using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using UserService.Application.Mappings;
using UserService.Domain.Entities;

namespace UserService.Tests.Application.Mappings;

public sealed class UserMappingTests
{
    [Before(HookType.Test)]
    public void SetUp()
    {
    }

    [Test]
    public async Task ToDto_WithValidUser_MapsAllPropertiesCorrectly()
    {
        // Arrange - use Rehydrate with hardcoded hash/salt
        var hash = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var salt = new byte[] { 0x05, 0x06, 0x07, 0x08 };
        var user = User.Rehydrate(1, "TestUser", hash, salt, DateTime.UtcNow, DateTime.UtcNow);

        // Act
        var dto = user.ToReadDto();

        // Assert
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto.Id).IsEqualTo(user.Id);
        await Assert.That(dto.Username).IsEqualTo(user.Username);
        await Assert.That(dto.CreatedAt).IsEqualTo(user.CreatedAt);
        await Assert.That(dto.UpdatedAt).IsEqualTo(user.UpdatedAt);
    }

    [Test]
    public async Task ToDto_WhenDatesAreNull_MapsSuccessfully()
    {
        // Arrange - use Rehydrate with null dates
        var hash = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var salt = new byte[] { 0x05, 0x06, 0x07, 0x08 };
        var user = User.Rehydrate(
            1,
            "TestUser",
            hash,
            salt,
            createdAt: null,
            updatedAt: null);

        // Act
        var dto = user.ToReadDto();

        // Assert
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto.Id).IsEqualTo(1);
        await Assert.That(dto.Username).IsEqualTo("TestUser");
        await Assert.That(dto.CreatedAt).IsNull();
        await Assert.That(dto.UpdatedAt).IsNull();
    }

    /// <summary>
    /// ⚠️ KNOWN DEFECT — this test documents current behaviour, not correct behaviour.
    /// </summary>
    /// <remarks>
    /// <c>UserMapping.ToReadDto</c> has no null guard, so a null argument surfaces as a
    /// <see cref="NullReferenceException"/>. The correct fix is
    /// <c>ArgumentNullException.ThrowIfNull(user)</c> in the production mapping — but production
    /// changes are out of scope for this test-rewrite plan. <b>When that guard is added this test
    /// will fail, and the failure is the expected outcome</b>, not a regression: update it to
    /// expect <c>ArgumentNullException</c> with parameter name "user" at the same time.
    /// </remarks>
    [Test]
    public void ToDto_WhenUserIsNull_ThrowsNullReferenceException()
    {
        // Act & Assert - the extension method does not guard against null, so it throws NRE.
        Assert.Throws<NullReferenceException>(() => UserMapping.ToReadDto(null!));
    }
}
