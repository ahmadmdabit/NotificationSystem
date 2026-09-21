using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text;

using DAL.Entity;

using Dapper;

namespace DAL.Repository;

public abstract class BaseCompositeRepository<T, TKey1, TKey2> : RepositoryBase, ICompositeRepository<T, TKey1, TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    protected readonly IDbConnection DbConnection;
    protected static readonly string TableName = T.TableName;
    protected static readonly string Key1Col = T.Key1ColumnName;
    protected static readonly string Key2Col = T.Key2ColumnName;

    private static readonly string PrecomputedInsertSql;
    private static readonly string PrecomputedGetSql;
    private static readonly string PrecomputedDeleteSql;
    private static readonly string PrecomputedListSql;

    // property whitelist shared with the projection validator
    private static readonly IReadOnlySet<string> PropertySet;

    static BaseCompositeRepository()
    {
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p =>
            {
                var attr = p.GetCustomAttribute<DescriptionAttribute>(false);
                return attr == null || !string.Equals(attr.Description, "ignore", StringComparison.OrdinalIgnoreCase);
            })
            .Select(p => p.Name)
            .ToImmutableArray();

        PropertySet = props.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Build precomputed INSERT
        var cols = new StringBuilder();
        var vals = new StringBuilder();
        foreach (var p in props)
        {
            if (p is "CreatedAt" or "UpdatedAt" or "IsDeleted") continue;
            cols.Append($"[{p}],");
            vals.Append($"@{p},");
        }
        if (cols.Length > 0) cols.Length--;
        if (vals.Length > 0) vals.Length--;

        PrecomputedInsertSql = $"INSERT INTO [{TableName}] ({cols}) OUTPUT INSERTED.* VALUES ({vals});";
        // no IsDeleted read filter — nothing in the system ever sets IsDeleted=1 (all deletes
        // are hard), so reads match BaseRepository semantics exactly.
        PrecomputedGetSql = $"SELECT {{0}} FROM [{TableName}] WHERE [{Key1Col}] = @K1 AND [{Key2Col}] = @K2";
        PrecomputedDeleteSql = $"DELETE FROM [{TableName}] WHERE [{Key1Col}] = @K1 AND [{Key2Col}] = @K2";
        PrecomputedListSql = $"SELECT {{0}} FROM [{TableName}]";
    }

    protected BaseCompositeRepository(IDbConnection dbConnection)
    {
        DbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
    }

    public async Task<T?> GetAsync(TKey1 key1, TKey2 key2, string include = "*", CancellationToken cancellationToken = default)
    {
        // validate projection before interpolation — same contract as BaseRepository
        ValidateProjection(include, PropertySet);
        var sql = string.Format(PrecomputedGetSql, include);
        var cmd = new CommandDefinition(sql, new { K1 = key1, K2 = key2 }, cancellationToken: cancellationToken);
        return await DbConnection.QuerySingleOrDefaultAsync<T>(cmd).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all records — required by the UI history grid and the Ocelot
    /// GET /NotificationHistories route, which previously 404'd against the composite controller.
    /// </summary>
    public async Task<IEnumerable<T>> GetAsync(string include = "*", CancellationToken cancellationToken = default)
    {
        ValidateProjection(include, PropertySet);
        var sql = string.Format(PrecomputedListSql, include);
        var cmd = new CommandDefinition(sql, cancellationToken: cancellationToken);
        return await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(TKey1 key1, TKey2 key2, CancellationToken cancellationToken = default)
    {
        var cmd = new CommandDefinition(PrecomputedDeleteSql, new { K1 = key1, K2 = key2 }, cancellationToken: cancellationToken);
        return await DbConnection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public async Task<T> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        var cmd = new CommandDefinition(PrecomputedInsertSql, entity, cancellationToken: cancellationToken);
        return (await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false)).SingleOrDefault()!;
    }
}