using System.Data;

using Dapper;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAL.Extensions;

public static class ServiceCollectionExtensions
{
    private static int initialized;

    public static IServiceCollection AddDAL(this IServiceCollection services, IConfiguration Configuration)
    {
        // Thread-safe, atomic, one-time initialization
        if (Interlocked.Exchange(ref initialized, 1) == 0)
        {
            ConfigureDapper();
        }

        services.AddScoped<IDbConnection>(o => new SqlConnection(Configuration["AppSettings:SqlConnectionString"]));

        return services;
    }

    private static void ConfigureDapper()
    {
        SqlMapper.AddTypeMap(typeof(DateTime), DbType.DateTime2);
        SqlMapper.AddTypeMap(typeof(DateTime?), DbType.DateTime2);
    }
}