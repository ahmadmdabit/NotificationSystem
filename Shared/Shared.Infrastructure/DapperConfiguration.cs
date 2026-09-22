using Dapper;
using System.Data;

namespace Shared.Infrastructure;

/// <summary>
/// Configures Dapper global type mappings. Called once per process.
/// </summary>
public static class DapperConfiguration
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 0)
        {
            SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
            SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
        }
    }
}