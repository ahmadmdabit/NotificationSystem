CREATE OR ALTER PROCEDURE [dbo].[SPInsertNotification]
    @Title NVARCHAR(200),
    @Content NVARCHAR(MAX),
    @Status INT,
    @CreatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Notifications (Title, Content, Status, CreatedAt)
    OUTPUT INSERTED.*
    VALUES (@Title, @Content, @Status, @CreatedAt);
END
