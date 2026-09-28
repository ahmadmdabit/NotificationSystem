using System.Data;
using System.Data.Common;

namespace TestDoubles.Helpers;

public static class TestTransactionFactory
{
    /// <summary>
    /// Creates an ambient transaction for <c>CommandDefinition.Transaction</c>.
    /// </summary>
    /// <remarks>
    /// Must be a <see cref="DbTransaction"/>, not an <see cref="IDbTransaction"/>: Dapper assigns
    /// the ambient transaction to a <see cref="DbCommand"/>, whose setter casts to
    /// <see cref="DbTransaction"/>. Mock creation lives here so test projects never call
    /// <c>DbTransaction.Mock()</c> directly — that extension is generated in both this assembly
    /// and the consuming test assembly, which makes the call site ambiguous (CS0121).
    /// </remarks>
    public static IDbTransaction CreateAmbient() => DbTransaction.Mock().Object;
}
