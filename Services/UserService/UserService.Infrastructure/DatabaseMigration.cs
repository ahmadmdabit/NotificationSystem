using System.Reflection;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

using Shared.Infrastructure;

namespace UserService.Infrastructure;

public class DatabaseMigration : DatabaseMigrationBase
{
    public DatabaseMigration(IConfiguration configuration) : base(configuration) { }

    protected override async Task CreateSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
                CREATE TABLE [dbo].[Users] (
                    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY NOT NULL,
                    [Username] NVARCHAR(256) NOT NULL,
                    [Token] NVARCHAR(MAX) NULL,
                    [PasswordHash] VARBINARY(MAX) NULL,
                    [PasswordSalt] VARBINARY(MAX) NULL,
                    [CreatedAt] DATETIME2 NULL,
                    [UpdatedAt] DATETIME2 NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0
                );

                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UXUsersUsername' AND object_id = OBJECT_ID('dbo.Users'))
                -- Unique index on Username prevents TOCTOU race condition on registration (HIGH-04)
                CREATE UNIQUE NONCLUSTERED INDEX [UXUsersUsername] ON [dbo].[Users] ([Username]);");

        // Add columns if they don't exist (idempotent drift repair for pre-existing tables)
        await connection.ExecuteAsync(@"
                IF COL_LENGTH('dbo.Users', 'IsDeleted') IS NULL
                ALTER TABLE [dbo].[Users] ADD [IsDeleted] BIT NOT NULL DEFAULT 0;
                IF COL_LENGTH('dbo.Users', 'UpdatedAt') IS NULL
                ALTER TABLE [dbo].[Users] ADD [UpdatedAt] DATETIME2 NULL;");

        // Execute embedded stored procedures
        await ExecuteEmbeddedStoredProceduresAsync(connection, cancellationToken);
    }

    private async Task ExecuteEmbeddedStoredProceduresAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = new[]
        {
            "UserService.Infrastructure.Persistence.StoredProcedures.SPRegisterUser.sql",
            "UserService.Infrastructure.Persistence.StoredProcedures.SPAuthenticateUser.sql"
        };

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);
            await connection.ExecuteAsync(sql);
        }
    }
}