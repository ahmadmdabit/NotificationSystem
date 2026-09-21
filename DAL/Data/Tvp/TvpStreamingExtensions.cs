using Microsoft.Data.SqlClient.Server;

namespace DAL.Data.Tvp;

/// <summary>
/// Provides zero-allocation streaming wrappers for Dapper TVP consumption.
/// </summary>
public static class TvpStreamingExtensions
{
    /// <summary>
    /// Streams an enumerable directly as an IEnumerable of SqlDataRecord, reusing a single record buffer.
    /// </summary>
    public static IEnumerable<SqlDataRecord> AsSqlDataRecords<T>(
        this IEnumerable<T>? source,
        ITvpDefinition<T> definition)
    {
        if (source is null)
            yield break;

        var record = new SqlDataRecord(definition.Metadata);

        foreach (var item in source)
        {
            if (item is null)
                continue;

            definition.PopulateRecord(record, in item);
            yield return record;
        }
    }
}