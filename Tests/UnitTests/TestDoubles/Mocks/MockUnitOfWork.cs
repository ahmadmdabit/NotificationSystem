using System.Data;
using System.Data.Common;

using Shared.Application.Abstractions;

namespace TestDoubles.Mocks;

public static class MockUnitOfWork
{
    /// <param name="transaction">
    /// The ambient transaction to expose. Defaults to a <see cref="DbTransaction"/> mock rather than
    /// an <see cref="IDbTransaction"/> one: Dapper assigns the ambient transaction to a
    /// <see cref="DbCommand"/>, whose setter casts to <see cref="DbTransaction"/>. An
    /// <see cref="IDbTransaction"/> mock throws <see cref="InvalidCastException"/> there.
    /// </param>
    public static IUnitOfWorkMock Create(object? transaction = null)
    {
        var mock = IUnitOfWork.Mock();
        var tx = transaction ?? DbTransaction.Mock().Object;

        mock.Transaction.Returns(tx as IDbTransaction);
        mock.BeginTransactionAsync(Any<CancellationToken>()).Returns(() => Task.CompletedTask);
        mock.CommitAsync(Any<CancellationToken>()).Returns(() => Task.CompletedTask);
        mock.RollbackAsync(Any<CancellationToken>()).Returns(() => Task.CompletedTask);
        mock.SaveChangesAsync(Any<CancellationToken>()).Returns(() => Task.CompletedTask);

        return mock;
    }
}
