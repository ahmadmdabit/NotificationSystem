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
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(IDbConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public IDbTransaction? Transaction => _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        if (_connection.State != ConnectionState.Open)
        {
            if (_connection is SqlConnection sqlConnection)
                await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            else
                _connection.Open();
        }

        _transaction = _connection.BeginTransaction();
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction in progress.");
        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction in progress.");
        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
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
        if (!_disposed)
        {
            _transaction?.Dispose();
            _transaction = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
