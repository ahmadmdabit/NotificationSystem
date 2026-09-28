using System.Data.Common;

using Dapper;

namespace TestDoubles.Helpers;

/// <summary>
/// Extracts wire-level parameter names from a Dapper <see cref="CommandDefinition"/>, normalised
/// to a leading '@' so assertions read as the names SQL Server actually sees.
/// </summary>
/// <remarks>
/// <para>
/// Three repository test classes each carried their own copy of this logic under two different
/// names (<c>ParameterNames</c> and <c>ParamNamesOf</c>) with subtly different null behaviour
/// (R-21). They are now one implementation, so a fix to any branch reaches all of them.
/// </para>
/// <para>
/// The three parameter shapes are exhaustive for this repository, because every command is built
/// by a <c>*CommandFactory</c>:
/// </para>
/// <list type="bullet">
/// <item><c>null</c> — a read with no WHERE parameters (e.g. <c>SelectAll</c>).</item>
/// <item><c>DynamicParameters</c> — the stored-procedure commands.</item>
/// <item><c>DbParameterCollection</c> — the TVP bulk insert.</item>
/// <item>
/// Anything else is an anonymous projection. Dapper reflects over its properties, so the property
/// names <i>are</i> the wire parameter names — the same rule the SqlException 8144 contract guard
/// relies on.
/// </item>
/// </list>
/// <para>
/// <b>Deliberately not asserted against a production constant.</b> The name list is derived by
/// reflection from the object under test, never from a column constant, so mutating a projection
/// cannot leave a test green.
/// </para>
/// </remarks>
public static class CommandParameterNames
{
    public static IReadOnlyList<string> Of(CommandDefinition command)
    {
        // No null guard: CommandDefinition is non-nullable by contract and the repository
        // factories never yield null, so a guard here would only be an unreachable no-op (CA2264).
        return Of(command.Parameters);
    }

    public static IReadOnlyList<string> Of(object? parameters) => parameters switch
    {
        null => [],
        DynamicParameters dynamic =>
            dynamic.ParameterNames.Select(Normalise).ToList(),
        DbParameterCollection collection =>
            collection.Cast<DbParameter>().Select(p => p.ParameterName ?? string.Empty).ToList(),
        _ => parameters.GetType()
            .GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0)
            .Select(p => "@" + p.Name)
            .ToList()
    };

    /// <summary>
    /// Dapper stores names unprefixed ("Username"); the '@' is added when the parameter is bound
    /// to the <c>SqlParameter</c>. Normalise so assertions read as the wire-level name.
    /// </summary>
    private static string Normalise(string name) => "@" + name.TrimStart('@');
}
