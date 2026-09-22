using System.Data;
using Microsoft.Data.SqlClient;
using UserService.Domain.Abstractions;

namespace UserService.Infrastructure.Repositories;

/// <summary>
/// Unit of work wrapping IDbConnection + IDbTransaction for multi-repo transaction coordination.
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

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
        _transaction = _connection.BeginTransaction();
        await Task.CompletedTask;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction to commit.");
        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No transaction to roll back.");
        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
        await Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dapper is not EF Core — SaveChanges is a no-op (each ExecuteAsync auto-commits).
        // This exists for interface conformance and to allow future repository impls that batch.
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
