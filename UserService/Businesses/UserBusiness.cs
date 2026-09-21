using System.Security.Cryptography;

using BLL.Business;

using DAL.Repository;

using UserService.Entities;
using UserService.Models;

namespace UserService.Businesses;

public class UserBusiness : BaseBusiness<User, long>
{
    // OWASP recommended minimum for PBKDF2-HMAC-SHA512
    private const int SaltByteSize = 32;       // 256-bit salt
    private const int HashByteSize = 64;       // 512-bit hash
    private const int Iterations = 600000;

    public UserBusiness(IRepository<User, long> repository) : base(repository)
    {
    }

    // True overrides of the generic base — restores the safe column projection that
    // prevents PasswordHash/PasswordSalt/Token from leaking through GET /api/Users.
    public override async Task<IEnumerable<User>> GetAsync(string include = "*", CancellationToken cancellationToken = default)
    {
        return await this.Repository.GetAsync("Id, Username, CreatedAt, UpdatedAt, IsDeleted", cancellationToken).ConfigureAwait(false);
    }

    public override async Task<User> GetAsync(long id, string include = "*", CancellationToken cancellationToken = default)
    {
        return await this.Repository.GetAsync(id, "Id, Username, CreatedAt, UpdatedAt, IsDeleted", cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> RegisterAsync(RegisterModel model)
    {
        if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            return null;
        var entities = (await this.Repository.GetAsync("Username", model.Username).ConfigureAwait(false))?.ToList();
        if (entities?.Count > 0)
        {
            return null;
        }
        byte[] passwordHash, passwordSalt;
        CreatePasswordHash(model.Password, out passwordHash, out passwordSalt);
        return await this.Repository.InsertAsync(new User { Username = model.Username, PasswordHash = passwordHash, PasswordSalt = passwordSalt }).ConfigureAwait(false);
    }

    public async Task<User?> AuthenticateAsync(AuthenticateModel model)
    {
        if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            return null;

        var entities = (await this.Repository.GetAsync("Username", model.Username).ConfigureAwait(false))?.ToList();
        if (entities?.Count > 0)
        {
            if (VerifyPasswordHash(model.Password, entities[0].PasswordHash, entities[0].PasswordSalt))
                return entities[0];
        }
        return null;
    }

    // PBKDF2-HMAC-SHA512 password hashing with configurable work factor
    private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(password));

        using (var rng = RandomNumberGenerator.Create())
        {
            passwordSalt = new byte[SaltByteSize];
            rng.GetBytes(passwordSalt);
        }

        passwordHash = new byte[HashByteSize];

        Rfc2898DeriveBytes.Pbkdf2(
            password,               // ReadOnlySpan<char> or ReadOnlySpan<byte>
            passwordSalt,           // ReadOnlySpan<byte>
            passwordHash,           // Span<byte> destination
            Iterations,
            HashAlgorithmName.SHA512);
    }

    // Constant-time password verification prevents timing attacks
    private static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
    {
        if (password == null) throw new ArgumentNullException(nameof(password));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Value cannot be empty or whitespace only string.", nameof(password));
        if (storedHash == null || storedHash.Length != HashByteSize) throw new ArgumentException($"Invalid length of password hash ({HashByteSize} bytes expected).", nameof(storedHash));
        if (storedSalt == null || storedSalt.Length != SaltByteSize) throw new ArgumentException($"Invalid length of password salt ({SaltByteSize} bytes expected).", nameof(storedSalt));

        byte[] computedHash = new byte[HashByteSize];

        Rfc2898DeriveBytes.Pbkdf2(
            password,               // ReadOnlySpan<char> or ReadOnlySpan<byte>
            storedSalt,           // ReadOnlySpan<byte>
            computedHash,           // Span<byte> destination
            Iterations,
            HashAlgorithmName.SHA512);

        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}