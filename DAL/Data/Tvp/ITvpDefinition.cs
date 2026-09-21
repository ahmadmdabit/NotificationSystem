using Microsoft.Data.SqlClient.Server;

namespace DAL.Data.Tvp;

/// <summary>
/// Defines a strongly-typed, high-performance TVP mapping contract.
/// </summary>
public interface ITvpDefinition<T>
{
    /// <summary>
    /// The database schema-qualified TVP user-defined table type name (e.g., "[dbo].[TypeNotificationHistory]").
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// The cached immutable metadata definitions defining column ordinals and types.
    /// </summary>
    SqlMetaData[] Metadata { get; }

    /// <summary>
    /// Maps a typed source entity onto a reusable SqlDataRecord instance without boxing.
    /// </summary>
    void PopulateRecord(SqlDataRecord record, in T source);
}
