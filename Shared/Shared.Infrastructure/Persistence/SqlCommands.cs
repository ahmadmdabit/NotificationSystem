using System.Data;
using System.Text;
using System.Text.RegularExpressions;

using Dapper;

namespace Shared.Infrastructure.Persistence;

/// <summary>
/// The SQL patterns that are genuinely identical across services: <c>IsDeleted</c>-guarded
/// projections and soft deletes. <c>UserCommandFactory</c> and
/// <c>NotificationCommandFactory</c> both build these, so the soft-delete convention
/// lives in exactly one place and a new table cannot accidentally omit the guard.
/// </summary>
/// <remarks>
/// <para>
/// The two factories are named in <c>&lt;c&gt;</c> rather than <c>&lt;see cref&gt;</c> because
/// <c>Shared.Infrastructure</c> references neither service project — and must not, or the
/// dependency would invert Clean Architecture. The crefs could not resolve, and the failure was
/// silent because <c>GenerateDocumentationFile</c> is off repo-wide, so CS1574 is never emitted.
/// </para>
/// <para>
/// <b>Stored-procedure invocations deliberately stay in each service's own
/// <c>*CommandFactory</c>.</b> Their parameter shapes differ per domain (explicit
/// <see cref="DynamicParameters"/> with sizes vs anonymous projections, TVPs, output
/// parameters), so folding them in here would mean a generic bag of parameters and would
/// make the SP contract less readable, not more.
/// </para>
/// <para>
/// <b>No caller ever supplies a SQL fragment.</b> Callers pass <i>identifiers</i> (table and
/// column names) and the matching parameter object; this type builds every predicate. The
/// identifiers are validated against <see cref="IdentifierPattern"/> because they are
/// interpolated into SQL and therefore cannot be parameterised. This honours the
/// AGENTS.md rule that projections be validated with a compiled regex — a rule that was
/// documented but had no implementation until now.
/// </para>
/// </remarks>
public static partial class SqlCommands
{
    /// <summary>
    /// A bare SQL identifier: a letter or underscore followed by letters, digits or
    /// underscores. Anything else (spaces, quotes, semicolons, comments) is rejected.
    /// </summary>
    /// <remarks>
    /// Anchored with <c>\A</c>/<c>\z</c>, NOT <c>^</c>/<c>$</c>. In .NET a bare <c>$</c> also
    /// matches immediately before a trailing newline, so <c>^[a-z]+$</c> accepts
    /// <c>"Users\n"</c> — which would then be interpolated into the SQL text. That was a real
    /// bypass here, caught by adding <c>"Users\n"</c> to the rejection cases.
    /// </remarks>
    [GeneratedRegex(@"\A[a-zA-Z_][a-zA-Z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    /// <summary>Rejects a value that is not a bare identifier, naming the offending argument.</summary>
    /// <exception cref="ArgumentException">The value is not a valid identifier.</exception>
    public static string ValidateIdentifier(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (!IdentifierPattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid SQL identifier. Only [a-zA-Z_][a-zA-Z0-9_]* is allowed, " +
                "because identifiers are interpolated into SQL and cannot be parameterised.",
                parameterName);
        }

        return value;
    }

    private static string JoinColumns(IReadOnlyList<string> columns, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(columns, parameterName);

        if (columns.Count == 0)
            throw new ArgumentException("At least one column is required.", parameterName);

        var builder = new StringBuilder();
        for (var i = 0; i < columns.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(ValidateIdentifier(columns[i], parameterName));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds <c>WHERE {col} = @{col} AND … AND IsDeleted = 0</c> from key column names.
    /// The caller must supply a parameter object exposing a member per key, with the
    /// matching name — Dapper binds by name, so the two must agree.
    /// </summary>
    private static string BuildKeyPredicate(IReadOnlyList<string> keyColumns, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(keyColumns, parameterName);

        if (keyColumns.Count == 0)
            throw new ArgumentException("At least one key column is required.", parameterName);

        var builder = new StringBuilder();
        foreach (var column in keyColumns)
        {
            var validated = ValidateIdentifier(column, parameterName);
            builder.Append(validated).Append(" = @").Append(validated).Append(" AND ");
        }

        builder.Append("IsDeleted = 0");
        return builder.ToString();
    }

    // ---- Projections ---------------------------------------------------------------

    /// <summary>
    /// The <c>SELECT … FROM …</c> prefix shared by every read: the projection is validated and
    /// joined once, so no caller can hand-roll a projection that skips
    /// <see cref="ValidateIdentifier"/>.
    /// </summary>
    public static string BuildSelect(string table, IReadOnlyList<string> columns)
        => $"SELECT {JoinColumns(columns, nameof(columns))} FROM {ValidateIdentifier(table, nameof(table))}";

    /// <summary>Single-key read guarded against soft-deleted rows.</summary>
    public static CommandDefinition SelectByKey(
        string table,
        IReadOnlyList<string> columns,
        object parameters,
        IReadOnlyList<string> keyColumns,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => new(
            $"{BuildSelect(table, columns)} WHERE {BuildKeyPredicate(keyColumns, nameof(keyColumns))}",
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

    /// <summary>Unfiltered read of every non-deleted row.</summary>
    public static CommandDefinition SelectAll(
        string table,
        IReadOnlyList<string> columns,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => new(
            $"{BuildSelect(table, columns)} WHERE IsDeleted = 0",
            transaction: transaction,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Set-membership read: <c>… WHERE {column} IN @{parameterName} AND IsDeleted = 0</c>.
    /// The column is validated and the values stay bound, so the caller cannot inject either.
    /// </summary>
    public static CommandDefinition SelectIn(
        string table,
        IReadOnlyList<string> columns,
        object parameters,
        string keyColumn,
        string parameterName,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => new(
            $"{BuildSelect(table, columns)} WHERE {ValidateIdentifier(keyColumn, nameof(keyColumn))} IN @{ValidateIdentifier(parameterName, nameof(parameterName))} AND IsDeleted = 0",
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

    // ---- Soft delete ---------------------------------------------------------------

    /// <summary>
    /// Flags rows deleted rather than removing them, so history survives. Returns a command
    /// that affects 0 rows when every target is already deleted, which is what makes a
    /// repeat delete a no-op.
    /// </summary>
    public static CommandDefinition SoftDeleteByKey(
        string table,
        object parameters,
        IReadOnlyList<string> keyColumns,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
        => new(
            $"UPDATE {ValidateIdentifier(table, nameof(table))} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE {BuildKeyPredicate(keyColumns, nameof(keyColumns))}",
            parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);
}
