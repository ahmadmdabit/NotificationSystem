using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Shared.Infrastructure;

/// <summary>
/// Base class for database migrations. Handles database creation and retry logic.
/// Derived classes implement schema creation (tables, types, procedures).
/// </summary>
public abstract class DatabaseMigrationBase : IHostedService
{
    private readonly string connectionString;

    protected DatabaseMigrationBase(IConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration["AppSettings:SqlConnectionString"]);

        connectionString = configuration["AppSettings:SqlConnectionString"]!;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        // CREATE DATABASE cannot be parameterized, so the name is interpolated. It comes from
        // the operator's own connection string (not user input), but it is still escaped below:
        // the IF-NOT-EXISTS literal doubles any single quote, and the bracket identifier
        // doubles a closing bracket. Normal names are emitted byte-identically.
        builder.InitialCatalog = "master";
        var masterConnectionString = builder.ConnectionString;

        await RetryAsync(async () =>
        {
            await using var masterConn = new SqlConnection(masterConnectionString);
            await masterConn.OpenAsync(cancellationToken);
            // CREATE DATABASE cannot be parameterized, so the name is interpolated. The value
            // comes from the operator's own connection string, but it is still escaped: doubling
            // any embedded single quote makes the string literal well-formed instead of
            // terminable, and leaves every normal (quote-free) database name byte-identical (N-06).
            var escapedDatabaseName = databaseName.Replace("'", "''");
            await masterConn.ExecuteAsync($@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{escapedDatabaseName}')
                        CREATE DATABASE [{databaseName.Replace("]", "]]")}]");
        }, cancellationToken);

        await RetryAsync(async () =>
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);
            await CreateSchemaAsync(conn, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Creates database schema (tables, types, stored procedures).
    /// </summary>
    protected abstract Task CreateSchemaAsync(SqlConnection connection, CancellationToken cancellationToken);

    protected static async Task RetryAsync(Func<Task> work, CancellationToken cancellationToken, int maxAttempts = 3)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { await work(); return; }
            catch (Exception) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
