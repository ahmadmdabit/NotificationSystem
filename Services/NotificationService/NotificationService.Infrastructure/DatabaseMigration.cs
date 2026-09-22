using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Shared.Infrastructure;
using System.Reflection;
using System.IO;

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
                        INSERT INTO [dbo].[NotificationHistories] (NotificationId, UserId, CreatedAt, IsDeleted)
                        SELECT NotificationId, UserId, GETUTCDATE(), 0 FROM @Entities;
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
            "NotificationService.Infrastructure.Persistence.StoredProcedures.sp_InsertNotification.sql",
            "NotificationService.Infrastructure.Persistence.StoredProcedures.sp_UpdateNotification.sql"
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