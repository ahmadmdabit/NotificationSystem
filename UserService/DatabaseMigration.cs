using Dapper;

using Microsoft.Data.SqlClient;

namespace UserService;

public class DatabaseMigration : DAL.DatabaseMigrationBase
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

                -- Unique index on Username prevents TOCTOU race condition on registration (HIGH-04)
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UXUsersUsername' AND object_id = OBJECT_ID('dbo.Users'))
                CREATE UNIQUE NONCLUSTERED INDEX [UXUsersUsername] ON [dbo].[Users] ([Username]);");
    }
}
