using Common.Helpers;
using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DAL.Repository
{
    public interface IRepository<T>
    {
        Task<IEnumerable<T>> GetAsync(string include = "*");

        Task<IEnumerable<T>> GetAsync(string prop, object value, string include = "*");

        Task<T> GetAsync(long id, string include = "*");

        Task<bool> DeleteAsync(long id);

        Task<int> InsertAsync(IEnumerable<T> list, string[] exclude = null);

        Task<T> InsertAsync(T t, string[] exclude = null);

        Task<T> UpdateAsync(T t, string[] exclude = null);

        Task<SpResult> QueryAsync(string sql, DynamicParameters parameters, CommandType commandType = CommandType.StoredProcedure);

        Task<SpResult> QueryMultipleAsync(string sql, DynamicParameters parameters, CommandType commandType = CommandType.StoredProcedure);
    }
}