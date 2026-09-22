using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Server;
using NotificationService.Domain;

namespace NotificationService.Infrastructure.Data.Tvp;

/// <summary>
/// Defines a strongly-typed, high-performance TVP mapping contract for NotificationHistory.
/// </summary>
public interface ITvpDefinition<T>
{
    string TypeName { get; }
    SqlMetaData[] Metadata { get; }
    void PopulateRecord(SqlDataRecord record, in T source);
}
