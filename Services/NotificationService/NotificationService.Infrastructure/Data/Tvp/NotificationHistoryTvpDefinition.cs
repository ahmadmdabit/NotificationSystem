using System.Data;
using System.Runtime.CompilerServices;

using Microsoft.Data.SqlClient.Server;

using NotificationService.Domain;

namespace NotificationService.Infrastructure.Data.Tvp;

/// <summary>
/// TVP definition for NotificationHistory entities.
/// </summary>
public sealed class NotificationHistoryTvpDefinition : ITvpDefinition<NotificationHistory>
{
    public static readonly NotificationHistoryTvpDefinition Instance = new();

    public string TypeName => "[dbo].[TypeNotificationHistory]";

    // Pre-allocated, thread-safe, immutable schema metadata
    public SqlMetaData[] Metadata { get; } =
    [
        new SqlMetaData("NotificationId", SqlDbType.BigInt),
        new SqlMetaData("UserId", SqlDbType.BigInt)
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopulateRecord(SqlDataRecord record, in NotificationHistory source)
    {
        record.SetInt64(0, source.NotificationId);
        record.SetInt64(1, source.UserId);
    }
}
