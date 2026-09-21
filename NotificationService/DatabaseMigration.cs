using Dapper;

using Microsoft.Data.SqlClient;

namespace NotificationService;

public class DatabaseMigration : DAL.DatabaseMigrationBase
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
                    [CreatedAt] DATETIME2 NULL,
                    [UpdatedAt] DATETIME2 NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0
                )");

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
    }
}
