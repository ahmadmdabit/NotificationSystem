CREATE OR ALTER PROCEDURE [dbo].[sp_AuthenticateUser]
    @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, Username, PasswordHash, PasswordSalt, CreatedAt, UpdatedAt
    FROM Users
    WHERE Username = @Username;
END
