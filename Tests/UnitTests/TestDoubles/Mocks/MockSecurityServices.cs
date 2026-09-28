using UserService.Domain.Abstractions;

namespace TestDoubles.Mocks;

public static class MockSecurityServices
{
    public static IPasswordHasherMock CreateHasher(out Arg<byte[]> hash, out Arg<byte[]> salt, bool verifyAlwaysSucceeds = true)
    {
        var mock = IPasswordHasher.Mock();

        var testHash = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var testSalt = new byte[] { 0x05, 0x06, 0x07, 0x08 };

        hash = Any<byte[]>();
        salt = Any<byte[]>();

        // Setup HashPassword to set out parameters via generated SetsOut methods
        mock.HashPassword(Any<string>())
            .SetsOutHash(testHash)
            .SetsOutSalt(testSalt);

        mock.VerifyPassword(Any<string>(), hash, salt)
            .Returns(verifyAlwaysSucceeds);

        return mock;
    }

    public static ITokenServiceMock CreateTokenService(string fixedToken = "test-jwt-token")
    {
        var mock = ITokenService.Mock();

        mock.GenerateToken(Any<long>(), Any<string?>())
            .Returns((long userId, string? role) =>
                role is null ? $"{fixedToken}-{userId}" : $"{fixedToken}-{userId}-{role}");

        return mock;
    }
}
