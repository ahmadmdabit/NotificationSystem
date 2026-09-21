using System.Collections.Immutable;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Common.Helpers;

using DAL.Entity;

using Dapper;

namespace DAL.Repository;

/// <summary>
/// Non-generic base class responsible for process-wide, one-time DAL configuration
/// and shared SQL identifier/projection validation.
/// </summary>
public abstract partial class RepositoryBase
{
    static RepositoryBase()
    {
        // Configures Dapper globally to map DateTime to SQL Server's datetime2 (prevents precision truncation)
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    internal static bool IsValidIdentifier(string identifier) => IdentifierRegex().IsMatch(identifier);

    /// <summary>
    /// Validates a comma-separated column projection against the entity's property set.
    /// Shared by single-key and composite repositories to close the SQL injection surface uniformly.
    /// </summary>
    internal static void ValidateProjection(string include, IReadOnlySet<string> propertySet)
    {
        if (string.IsNullOrWhiteSpace(include))
            throw new ArgumentException("Projection cannot be empty.", nameof(include));

        if (include == "*") return;

        var tokens = include.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (!IsValidIdentifier(token) || !propertySet.Contains(token))
            {
                throw new ArgumentException($"Invalid projection field: '{token}'", nameof(include));
            }
        }
    }
}

public abstract class BaseRepository<T, TKey> : RepositoryBase, IRepository<T, TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    protected readonly IDbConnection DbConnection;
    protected static readonly string TableName = T.TableName;
    protected static readonly string KeyColumn = T.KeyColumnName;

    // Cache property names and metadata once per closed generic type
    private static readonly ImmutableArray<string> CachedProperties;
    private static readonly IReadOnlySet<string> PropertySet;
    private static readonly string PrecomputedDefaultInsertSql;
    private static readonly string PrecomputedDefaultUpdateSql;

    static BaseRepository()
    {
        // Extract properties not marked with [Description("ignore")]
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p =>
            {
                var attr = p.GetCustomAttribute<DescriptionAttribute>(false);
                return attr == null || !string.Equals(attr.Description, "ignore", StringComparison.OrdinalIgnoreCase);
            })
            .Select(p => p.Name)
            .ToImmutableArray();

        CachedProperties = props;
        PropertySet = props.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Precompute default queries at JIT load time
        var defaultExcludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KeyColumn, "CreatedAt", "UpdatedAt", "IsDeleted"
        };

        PrecomputedDefaultInsertSql = BuildInsertSql(defaultExcludes);
        PrecomputedDefaultUpdateSql = BuildUpdateSql(defaultExcludes);
    }

    protected BaseRepository(IDbConnection dbConnection)
    {
        DbConnection = dbConnection ?? throw new ArgumentNullException(nameof(dbConnection));
    }

    public virtual async Task<IEnumerable<T>> GetAsync(string include = "*", CancellationToken cancellationToken = default)
    {
        ValidateProjection(include, PropertySet);
        var cmd = new CommandDefinition($"SELECT {include} FROM [{TableName}]", cancellationToken: cancellationToken);
        return await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false);
    }

    public virtual async Task<IEnumerable<T>> GetAsync(string prop, object value, string include = "*", CancellationToken cancellationToken = default)
    {
        if (!IsValidIdentifier(prop) || !PropertySet.Contains(prop))
            throw new ArgumentException($"Invalid column property: '{prop}'", nameof(prop));

        ValidateProjection(include, PropertySet);

        var parameters = new DynamicParameters();
        parameters.Add("Val", value);

        var cmd = new CommandDefinition(
            $"SELECT {include} FROM [{TableName}] WHERE [{prop}] = @Val",
            parameters,
            cancellationToken: cancellationToken);

        return await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false);
    }

    public virtual async Task<T> GetAsync(TKey id, string include = "*", CancellationToken cancellationToken = default)
    {
        ValidateProjection(include, PropertySet);

        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        var cmd = new CommandDefinition(
            $"SELECT {include} FROM [{TableName}] WHERE [{KeyColumn}] = @Id",
            parameters,
            cancellationToken: cancellationToken);

        var result = await DbConnection.QuerySingleOrDefaultAsync<T>(cmd).ConfigureAwait(false);
        return result ?? throw new KeyNotFoundException($"Entity '{TableName}' with key [{id}] could not be found.");
    }

    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        var cmd = new CommandDefinition(
            $"DELETE FROM [{TableName}] WHERE [{KeyColumn}] = @Id",
            parameters,
            cancellationToken: cancellationToken);

        return await DbConnection.ExecuteAsync(cmd).ConfigureAwait(false) > 0;
    }

    public virtual async Task<int> InsertBatchAsync(IEnumerable<T> list, CancellationToken cancellationToken = default)
    {
        string sql = BuildBatchInsertSql(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KeyColumn, "CreatedAt", "UpdatedAt", "IsDeleted"
        });

        var cmd = new CommandDefinition(sql, list, cancellationToken: cancellationToken);
        return await DbConnection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    public virtual async Task<T> InsertAsync(T entity, string[]? exclude = null, CancellationToken cancellationToken = default)
    {
        string sql = exclude is null ? PrecomputedDefaultInsertSql : BuildInsertSql(exclude.ToHashSet(StringComparer.OrdinalIgnoreCase));
        var cmd = new CommandDefinition(sql, entity, cancellationToken: cancellationToken);
        return (await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false)).SingleOrDefault()!;
    }

    public virtual async Task<T> UpdateAsync(T entity, string[]? exclude = null, CancellationToken cancellationToken = default)
    {
        string sql = exclude is null ? PrecomputedDefaultUpdateSql : BuildUpdateSql(exclude.ToHashSet(StringComparer.OrdinalIgnoreCase));
        var cmd = new CommandDefinition(sql, entity, cancellationToken: cancellationToken);
        return (await DbConnection.QueryAsync<T>(cmd).ConfigureAwait(false)).SingleOrDefault()!;
    }

    public virtual async Task<SpResult> QueryAsync(
        string sql,
        DynamicParameters? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        CancellationToken cancellationToken = default)
    {
        var cmd = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: cancellationToken);
        var results = await DbConnection.QueryAsync(cmd).ConfigureAwait(false);

        return new SpResult
        {
            Success = parameters?.Get<bool>("SPSuccess") == true,
            Message = parameters?.Get<string>("SPMessage"),
            Data = results.AsList()
        };
    }

    public virtual async Task<SpResult> QueryMultipleAsync(
        string sql,
        DynamicParameters? parameters = null,
        CommandType commandType = CommandType.StoredProcedure,
        CancellationToken cancellationToken = default)
    {
        var cmd = new CommandDefinition(sql, parameters, commandType: commandType, cancellationToken: cancellationToken);
        using var grid = await DbConnection.QueryMultipleAsync(cmd).ConfigureAwait(false);

        var data = new List<object>();

        while (!grid.IsConsumed)
        {
            var resultSet = await grid.ReadAsync().ConfigureAwait(false);
            if (resultSet != null)
            {
                data.Add(resultSet.AsList());
            }
        }

        return new SpResult
        {
            Success = parameters?.Get<bool>("SPSuccess") == true,
            Message = parameters?.Get<string>("SPMessage"),
            Data = data.Count == 1 ? data[0] : data
        };
    }

    private static string BuildInsertSql(IReadOnlySet<string> excludes)
    {
        // shared builder — OUTPUT INSERTED.* returns the row without a second round-trip
        return BuildInsertCore(excludes, includeOutputClause: true);
    }

    private static string BuildBatchInsertSql(IReadOnlySet<string> excludes)
    {
        // batch variant omits the OUTPUT clause (multi-row ExecuteAsync)
        return BuildInsertCore(excludes, includeOutputClause: false);
    }

    private static string BuildInsertCore(IReadOnlySet<string> excludes, bool includeOutputClause)
    {
        var cols = new StringBuilder();
        var vals = new StringBuilder();

        for (int i = 0; i < CachedProperties.Length; i++)
        {
            string prop = CachedProperties[i];
            if (!excludes.Contains(prop))
            {
                cols.Append($"[{prop}],");
                vals.Append($"@{prop},");
            }
        }

        if (cols.Length > 0) cols.Length--;
        if (vals.Length > 0) vals.Length--;

        return includeOutputClause
            ? $"INSERT INTO [{TableName}] ({cols}) OUTPUT INSERTED.* VALUES ({vals});"
            : $"INSERT INTO [{TableName}] ({cols}) VALUES ({vals});";
    }

    private static string BuildUpdateSql(IReadOnlySet<string> excludes)
    {
        var sb = new StringBuilder($"UPDATE [{TableName}] SET ");

        for (int i = 0; i < CachedProperties.Length; i++)
        {
            string prop = CachedProperties[i];
            if (!excludes.Contains(prop) && !string.Equals(prop, KeyColumn, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append($"[{prop}]=@{prop},");
            }
        }

        if (sb.Length > 0) sb.Length--;
        sb.Append($" OUTPUT INSERTED.* WHERE [{KeyColumn}]=@{KeyColumn};");
        return sb.ToString();
    }
}