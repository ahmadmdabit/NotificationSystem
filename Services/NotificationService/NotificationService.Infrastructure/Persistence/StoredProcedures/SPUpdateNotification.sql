CREATE OR ALTER PROCEDURE [dbo].[SPUpdateNotification]
    @Id BIGINT,
    @Title NVARCHAR(200),
    @Content NVARCHAR(MAX),
    @Status INT,
    @SentAt DATETIME2 NULL,
    @UpdatedAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Notifications
    SET Title = @Title,
        Content = @Content,
        Status = @Status,
        SentAt = @SentAt,
        UpdatedAt = @UpdatedAt
    WHERE Id = @Id AND IsDeleted = 0;
END
