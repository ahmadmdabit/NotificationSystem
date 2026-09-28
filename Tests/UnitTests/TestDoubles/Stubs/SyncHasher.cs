using UserService.Domain.Abstractions;

namespace TestDoubles.Stubs;

/// <summary>
/// Hand-written synchronous <see cref="IPasswordHasher"/>: no mock framework, no async suspension.
/// </summary>
/// <remarks>
/// Use this when a test needs a real <see cref="IPasswordHasher"/> instance rather than a mock
/// (for example when constructing an aggregate via <c>User.Create</c>).
/// <para>
/// Its XML doc previously said "use when TUnit.Mocks forks ExecutionContext and orphans
/// AsyncLocal writes". That advice was <b>wrong</b> — <c>RegisterUserCommandHandlerTests</c>
/// records the opposite: TUnit.Mocks does not fork the ExecutionContext, and the real cause of
/// orphaned <c>DomainEventCollector</c> writes is an <c>await</c> boundary inside the callee.
/// See <c>InMemoryUserRepository</c> for the matching note.
/// </para>
/// </remarks>
public sealed class SyncHasher : IPasswordHasher
{
    public void HashPassword(string password, out byte[] hash, out byte[] salt)
    {
        hash = new byte[32];
        salt = new byte[16];
    }

    public bool VerifyPassword(string password, byte[] hash, byte[] salt) => true;
}
