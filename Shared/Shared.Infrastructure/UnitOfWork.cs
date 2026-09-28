using System.Data;

using Microsoft.Data.SqlClient;

using Shared.Application.Abstractions;

namespace Shared.Infrastructure;

/// <summary>
/// Unit of work wrapping IDbConnection + IDbTransaction for multi-repo transaction coordination.
/// Single shared implementation (DRY) — used by both services.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection connection;
    private IDbTransaction? transaction;
    private bool disposed;

    public UnitOfWork(IDbConnection connection)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IDbTransaction? Transaction => transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        if (connection.State != ConnectionState.Open)
        {
            if (connection is SqlConnection sqlConnection)
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            else
                connection.Open();
        }

        transaction = connection.BeginTransaction();
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null)
            throw new InvalidOperationException("No transaction in progress.");
        transaction.Commit();
        transaction.Dispose();
        transaction = null;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (transaction is null)
            throw new InvalidOperationException("No transaction in progress.");
        transaction.Rollback();
        transaction.Dispose();
        transaction = null;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dapper is not EF Core — SaveChanges is a no-op (each ExecuteAsync auto-commits
        // unless enlisted in the ambient IUnitOfWork.Transaction).
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!disposed)
        {
            transaction?.Dispose();
            transaction = null;
            disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
