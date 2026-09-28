CREATE OR ALTER PROCEDURE [dbo].[SPAuthenticateUser]
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Username, PasswordHash, PasswordSalt, CreatedAt, UpdatedAt
    FROM Users
    WHERE Username = @Username AND IsDeleted = 0;
END
