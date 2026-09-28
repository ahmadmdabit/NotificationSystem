using System.Reflection;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

using Shared.Infrastructure;

namespace NotificationService.Infrastructure;

public class DatabaseMigration : DatabaseMigrationBase
{
    public DatabaseMigration(IConfiguration configuration) : base(configuration) { }

    protected override async Task CreateSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
                CREATE TABLE [dbo].[Notifications] (
                    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY NOT NULL,
                    [Title] NVARCHAR(512) NOT NULL,
                    [Content] NVARCHAR(MAX) NULL,
                    [Status] INT NOT NULL DEFAULT 0,
                    [SentAt] DATETIME2 NULL,
                    [CreatedAt] DATETIME2 NULL,
                    [UpdatedAt] DATETIME2 NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0
                )");

        // Add columns if they don't exist (idempotent drift repair)
        await connection.ExecuteAsync(@"
                IF COL_LENGTH('dbo.Notifications', 'Status') IS NULL
                ALTER TABLE [dbo].[Notifications] ADD [Status] INT NOT NULL DEFAULT 0;
                IF COL_LENGTH('dbo.Notifications', 'SentAt') IS NULL
                ALTER TABLE [dbo].[Notifications] ADD [SentAt] DATETIME2 NULL;");

        await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationHistories')
                CREATE TABLE [dbo].[NotificationHistories] (
                    [NotificationId] BIGINT NOT NULL,
                    [UserId] BIGINT NOT NULL,
                    [CreatedAt] DATETIME2 NULL,
                    [UpdatedAt] DATETIME2 NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    PRIMARY KEY ([NotificationId], [UserId])
                )");

        await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.types WHERE name = 'TypeNotificationHistory')
                CREATE TYPE [dbo].[TypeNotificationHistory] AS TABLE (
                    [NotificationId] BIGINT NOT NULL,
                    [UserId] BIGINT NOT NULL
                )");

        await connection.ExecuteAsync(@"
                CREATE OR ALTER PROCEDURE [dbo].[SPNotificationHistoryInsert]
                    @Entities [dbo].[TypeNotificationHistory] READONLY,
                    @SPSuccess BIT OUTPUT,
                    @SPMessage NVARCHAR(255) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRY
                        -- Idempotent by construction (N-07). NotificationHistories has
                        -- PRIMARY KEY (NotificationId, UserId), so an unguarded INSERT made a
                        -- second send of the same pair fail with a PK violation -> @SPSuccess = 0
                        -- -> repository throws -> HTTP 500. Filtering the TVP against existing
                        -- rows makes a repeat pair a silent no-op and the send genuinely
                        -- idempotent. The NOT EXISTS also covers soft-deleted rows (IsDeleted = 1),
                        -- which still occupy the key and must not be re-inserted.
                        INSERT INTO [dbo].[NotificationHistories] (NotificationId, UserId, CreatedAt, IsDeleted)
                        SELECT e.NotificationId, e.UserId, GETUTCDATE(), 0
                        FROM @Entities e
                        WHERE NOT EXISTS (
                            SELECT 1
                            FROM [dbo].[NotificationHistories] h
                            WHERE h.NotificationId = e.NotificationId
                              AND h.UserId = e.UserId
                        );
                        SET @SPSuccess = 1;
                        SET @SPMessage = 'Success';
                    END TRY
                    BEGIN CATCH
                        SET @SPSuccess = 0;
                        SET @SPMessage = ERROR_MESSAGE();
                    END CATCH
                END");

        // Execute embedded stored procedures
        await ExecuteEmbeddedStoredProceduresAsync(connection, cancellationToken);
    }

    private async Task ExecuteEmbeddedStoredProceduresAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = new[]
        {
            "NotificationService.Infrastructure.Persistence.StoredProcedures.SPInsertNotification.sql",
            "NotificationService.Infrastructure.Persistence.StoredProcedures.SPUpdateNotification.sql"
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