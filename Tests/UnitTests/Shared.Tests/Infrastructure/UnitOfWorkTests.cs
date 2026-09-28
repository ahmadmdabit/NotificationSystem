using Shared.Infrastructure;

using TestDoubles.Helpers;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Shared.Tests.Infrastructure;

public class UnitOfWorkTests
{
    [Test]
    public async Task Constructor_WhenConnectionNull_ThrowsArgumentNullException()
    {
        await Assert.That(() => new UnitOfWork(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task BeginTransactionAsync_WhenConnectionClosed_OpensConnectionAndStartsTransaction()
    {
        var db = TestConnectionFactory.CreateClosedConnection();
        var uow = new UnitOfWork(db.Connection);

        await uow.BeginTransactionAsync();

        await Assert.That(uow.Transaction).IsNotNull();
        db.ConnectionMock.Open().WasCalled(Times.Once);
        db.ConnectionMock.BeginTransaction().WasCalled(Times.Once);
    }

    [Test]
    public async Task BeginTransactionAsync_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var db = TestConnectionFactory.CreateOpenConnection();
        var uow = new UnitOfWork(db.Connection);
        await uow.BeginTransactionAsync();

        await Assert.That(() => uow.BeginTransactionAsync()).Throws<InvalidOperationException>();

        // The second attempt must not start another transaction
        db.ConnectionMock.BeginTransaction().WasCalled(Times.Once);
        db.ConnectionMock.Open().WasNeverCalled();
    }

    [Test]
    public async Task CommitAsync_WithoutActiveTransaction_ThrowsInvalidOperationException()
    {
        var db = TestConnectionFactory.CreatePlainConnection();
        var uow = new UnitOfWork(db.Connection);

        await Assert.That(() => uow.CommitAsync()).Throws<InvalidOperationException>();

        db.TransactionMock.Commit().WasNeverCalled();
    }

    [Test]
    public async Task CommitAsync_WithActiveTransaction_CommitsDisposesAndResetsTransaction()
    {
        var db = TestConnectionFactory.CreateOpenConnection();
        var uow = new UnitOfWork(db.Connection);
        await uow.BeginTransactionAsync();

        await Assert.That(uow.Transaction).IsNotNull();

        await uow.CommitAsync();

        await Assert.That(uow.Transaction).IsNull();
        db.TransactionMock.Commit().WasCalled(Times.Once);
        db.TransactionMock.Dispose().WasCalled(Times.Once);
        db.TransactionMock.Rollback().WasNeverCalled();
    }

    [Test]
    public async Task RollbackAsync_WithoutActiveTransaction_ThrowsInvalidOperationException()
    {
        var db = TestConnectionFactory.CreatePlainConnection();
        var uow = new UnitOfWork(db.Connection);

        await Assert.That(() => uow.RollbackAsync()).Throws<InvalidOperationException>();

        db.TransactionMock.Rollback().WasNeverCalled();
    }

    [Test]
    public async Task RollbackAsync_WithActiveTransaction_RollsBackDisposesAndResetsTransaction()
    {
        var db = TestConnectionFactory.CreateOpenConnection();
        var uow = new UnitOfWork(db.Connection);
        await uow.BeginTransactionAsync();

        await Assert.That(uow.Transaction).IsNotNull();

        await uow.RollbackAsync();

        await Assert.That(uow.Transaction).IsNull();
        db.TransactionMock.Rollback().WasCalled(Times.Once);
        db.TransactionMock.Dispose().WasCalled(Times.Once);
        db.TransactionMock.Commit().WasNeverCalled();
    }

    [Test]
    public async Task SaveChangesAsync_ExecutesAsNoOp()
    {
        var db = TestConnectionFactory.CreatePlainConnection();
        var uow = new UnitOfWork(db.Connection);

        await uow.SaveChangesAsync();

        await Assert.That(uow.Transaction).IsNull();
        db.ConnectionMock.BeginTransaction().WasNeverCalled();
    }

    [Test]
    public async Task Dispose_WithActiveTransaction_DisposesAndResetsTransaction()
    {
        var db = TestConnectionFactory.CreateOpenConnection();
        var uow = new UnitOfWork(db.Connection);
        await uow.BeginTransactionAsync();

        // Dispose() has side effects (disposes transaction)
        // Direct execution ensures the side effect happens before assertion
        uow.Dispose();

        await Assert.That(uow.Transaction).IsNull();
        db.TransactionMock.Dispose().WasCalled(Times.Once);
        db.TransactionMock.Commit().WasNeverCalled();
        db.TransactionMock.Rollback().WasNeverCalled();
    }

    [Test]
    public async Task Dispose_WhenCalledTwice_DisposesTransactionOnlyOnce()
    {
        var db = TestConnectionFactory.CreateOpenConnection();
        var uow = new UnitOfWork(db.Connection);
        await uow.BeginTransactionAsync();

        uow.Dispose();
        uow.Dispose();

        db.TransactionMock.Dispose().WasCalled(Times.Once);
    }

    [Test]
    public async Task BeginTransactionAsync_WhenNonSqlConnectionClosed_UsesSyncOpen()
    {
        var db = TestConnectionFactory.CreateClosedConnection();
        var uow = new UnitOfWork(db.Connection);

        await uow.BeginTransactionAsync();

        await Assert.That(uow.Transaction).IsNotNull();

        // The connection is a mocked IDbConnection, not a SqlConnection, so UnitOfWork must take
        // the synchronous Open() branch.
        db.ConnectionMock.Open().WasCalled(Times.Once);
    }
}
