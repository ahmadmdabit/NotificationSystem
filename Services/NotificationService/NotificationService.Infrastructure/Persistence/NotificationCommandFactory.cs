using System.Data;
using System.Data.Common;

using Dapper;

using NotificationService.Domain;
using NotificationService.Domain.Entities;
using NotificationService.Domain.ValueObjects;
using NotificationService.Infrastructure.Data.Tvp;

using Shared.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>
/// Builds the Dapper <see cref="CommandDefinition"/>s issued by
/// <see cref="Repositories.NotificationRepository"/> and
/// <see cref="Repositories.NotificationHistoryRepository"/>.
/// </summary>
/// <remarks>
/// The SQL contract lives here, separate from command execution, so it is directly testable.
/// Dapper's <c>QuerySingleOrDefaultAsync</c>/<c>ExecuteAsync</c> are <b>static extension methods</b>
/// on <see cref="IDbConnection"/> rather than interface members, so a test double on the connection
/// cannot intercept them, and the generated <see cref="DbCommand"/> mock exposes neither
/// <c>CreateParameter()</c> nor <c>Parameters</c> (both are protected). These builders are pure —
/// they build a command and return it without executing anything.
/// <para>
/// The projection and soft-delete shapes are shared with <c>UserCommandFactory</c> and are
/// delegated to <see cref="SqlCommands"/>. Stored procedures, the set-based Draft→Sent update
/// and the TVP bulk insert stay here because they are specific to this domain.
/// </para>
/// </remarks>
public static class NotificationCommandFactory
{
    /// <summary>Columns projected by the notification read queries, in hydration order.</summary>
    public static readonly IReadOnlyList<string> NotificationColumns =
        ["Id", "Title", "Content", "Status", "SentAt", "CreatedAt", "UpdatedAt"];

    /// <summary>Columns projected by the history read queries, in hydration order.</summary>
    public static readonly IReadOnlyList<string> HistoryColumns =
        ["NotificationId", "UserId", "CreatedAt", "UpdatedAt"];

    private static readonly IReadOnlyList<string> IdKey = ["Id"];
    private static readonly IReadOnlyList<string> HistoryKey = ["NotificationId", "UserId"];

    // ---- NotificationRepository -------------------------------------------------

    public static CommandDefinition GetNotificationById(long id, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectByKey("Notifications", NotificationColumns, new { Id = id }, IdKey, transaction, cancellationToken);

    public static CommandDefinition GetNotificationsByIds(IReadOnlyList<long> ids, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectIn(
            "Notifications",
            NotificationColumns,
            new { Ids = ids },
            keyColumn: "Id",
            parameterName: "Ids",
            transaction,
            cancellationToken);

    public static CommandDefinition GetAllNotifications(IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectAll("Notifications", NotificationColumns, transaction, cancellationToken);

    /// <summary>
    /// <c>Status</c> is sent as the numeric enum value, matching the SP's int parameter —
    /// an anonymous projection of <c>entity.Status</c> would emit <c>@Status</c> as a string
    /// and fail the conversion.
    /// </summary>
    public static CommandDefinition InsertNotification(Notification entity, IDbTransaction? transaction, CancellationToken cancellationToken)
        => new(
            "SPInsertNotification",
            new
            {
                entity.Title,
                entity.Content,
                Status = (int)entity.Status,
                entity.CreatedAt
            },
            transaction: transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

    public static CommandDefinition UpdateNotification(Notification entity, IDbTransaction? transaction, CancellationToken cancellationToken)
        => new(
            "SPUpdateNotification",
            new
            {
                entity.Id,
                entity.Title,
                entity.Content,
                Status = (int)entity.Status,
                entity.SentAt,
                entity.UpdatedAt
            },
            transaction: transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Set-based (M-4): one round trip for the whole batch instead of one
    /// SPUpdateNotification call per notification. The guards mirror the SP contract —
    /// IsDeleted = 0 plus Status = Draft — so a concurrent transition is never overwritten
    /// and re-sends stay idempotent.
    /// </summary>
    public static CommandDefinition MarkSentBatch(IReadOnlyList<long> ids, IDbTransaction? transaction, CancellationToken cancellationToken)
        => new(
            "UPDATE Notifications SET Status = @SentStatus, SentAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id IN @Ids AND IsDeleted = 0 AND Status = @DraftStatus",
            new
            {
                Ids = ids,
                SentStatus = (int)NotificationStatus.Sent,
                DraftStatus = (int)NotificationStatus.Draft
            },
            transaction: transaction,
            cancellationToken: cancellationToken);

    /// <summary>Soft delete — the row is flagged, never removed, so history survives.</summary>
    public static CommandDefinition SoftDeleteNotification(long id, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SoftDeleteByKey("Notifications", new { Id = id }, IdKey, transaction, cancellationToken);

    // ---- NotificationHistoryRepository -------------------------------------------

    public static CommandDefinition GetHistoryById(long notificationId, long userId, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectByKey(
            "NotificationHistories",
            HistoryColumns,
            new { NotificationId = notificationId, UserId = userId },
            HistoryKey,
            transaction,
            cancellationToken);

    public static CommandDefinition GetAllHistory(IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SelectAll("NotificationHistories", HistoryColumns, transaction, cancellationToken);

    public static CommandDefinition SoftDeleteHistory(long notificationId, long userId, IDbTransaction? transaction, CancellationToken cancellationToken)
        => SqlCommands.SoftDeleteByKey(
            "NotificationHistories",
            new { NotificationId = notificationId, UserId = userId },
            HistoryKey,
            transaction,
            cancellationToken);

    /// <summary>
    /// Bulk insert via TVP (single round trip). The caller streams the entities through
    /// <see cref="TvpStreamingExtensions.AsSqlDataRecords"/> and adds the output parameters
    /// that <c>SPNotificationHistoryInsert</c> uses to report success.
    /// </summary>
    public static DynamicParameters CreateHistoryBulkParameters(
        IReadOnlyList<NotificationHistory> entities,
        ITvpDefinition<NotificationHistory> tvpDefinition)
    {
        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Entities",
            value: entities.AsSqlDataRecords(tvpDefinition).AsTableValuedParameter(tvpDefinition.TypeName)
        );
        parameters.Add("@SPSuccess", value: null, dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@SPMessage", value: null, dbType: DbType.String, direction: ParameterDirection.Output, size: 255);
        return parameters;
    }

    public static CommandDefinition HistoryBulkInsert(
        DynamicParameters parameters,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => new(
            "[dbo].[SPNotificationHistoryInsert]",
            parameters,
            transaction: transaction,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);
}
