using System.Data;

namespace TestDoubles.Helpers;

/// <summary>
/// A mocked <see cref="IDbConnection"/> plus its transaction, bundling the mock wrappers with the
/// plain ADO.NET interfaces the code under test needs.
/// </summary>
/// <remarks>
/// Call sites pass <see cref="Connection"/> into production constructors, so they never write
/// <c>.Object</c>. Use <see cref="ConnectionMock"/> / <see cref="TransactionMock"/> when the test
/// needs to verify an interaction (for example
/// <c>db.TransactionMock.Commit().WasCalled(Times.Once)</c>).
/// </remarks>
public sealed record TestDbConnection
{
    internal TestDbConnection(IDbConnectionMock connection, IDbTransactionMock transaction)
    {
        ConnectionMock = connection;
        TransactionMock = transaction;
    }

    public IDbConnectionMock ConnectionMock { get; }

    public IDbTransactionMock TransactionMock { get; }

    /// <summary>The mocked connection as the interface production code depends on.</summary>
    public IDbConnection Connection => ConnectionMock.Object;

    /// <summary>The mocked transaction as the interface production code depends on.</summary>
    public IDbTransaction Transaction => TransactionMock.Object;
}

public static class TestConnectionFactory
{
    /// <summary>Connection reporting <see cref="ConnectionState.Open"/>.</summary>
    public static TestDbConnection CreateOpenConnection() => Create(ConnectionState.Open);

    /// <summary>Connection reporting <see cref="ConnectionState.Closed"/>, forcing the open path.</summary>
    public static TestDbConnection CreateClosedConnection() => Create(ConnectionState.Closed);

    /// <summary>Loose connection with no configured state or transaction.</summary>
    public static TestDbConnection CreatePlainConnection() => Create(default);

    private static TestDbConnection Create(ConnectionState state)
    {
        var transaction = IDbTransaction.Mock();
        var connection = IDbConnection.Mock();

        connection.State.Returns(state);
        connection.BeginTransaction().Returns(transaction.Object);

        return new TestDbConnection(connection, transaction);
    }
}
