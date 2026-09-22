using System.Data;

namespace Shared.Application.Abstractions;

/// <summary>
/// Unit of work for transaction coordination across repositories.
/// Single shared contract (DRY) — implemented by Shared.Infrastructure, consumed by
/// <c>TransactionBehavior</c> and the service Infrastructure repositories.
/// Lives in the Application layer (relocated from Shared.Domain, L-8) so
/// <c>System.Data.IDbTransaction</c> never leaks into a Domain layer.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDbTransaction? Transaction { get; }
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}