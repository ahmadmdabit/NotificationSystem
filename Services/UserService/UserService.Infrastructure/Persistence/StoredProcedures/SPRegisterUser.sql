CREATE OR ALTER PROCEDURE [dbo].[SPRegisterUser]
    @Username NVARCHAR(50),
    @PasswordHash BINARY(64),
    @PasswordSalt BINARY(32)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Users (Username, PasswordHash, PasswordSalt, CreatedAt)
    OUTPUT INSERTED.*
    VALUES (@Username, @PasswordHash, @PasswordSalt, GETUTCDATE());
END
