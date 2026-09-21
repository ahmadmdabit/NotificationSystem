using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DAL;

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

        // CREATE DATABASE cannot be parameterized; databaseName comes from
        // configuration (not user input) and is bracket-escaped.
        builder.InitialCatalog = "master";
        var masterConnectionString = builder.ConnectionString;

        await RetryAsync(async () =>
        {
            await using var masterConn = new SqlConnection(masterConnectionString);
            await masterConn.OpenAsync(cancellationToken);
            await masterConn.ExecuteAsync($@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}')
                        CREATE DATABASE [{databaseName}]");
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
