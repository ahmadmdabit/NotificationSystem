using System.Collections.Generic;
using System.Threading.Tasks;

namespace BLL.Business
{
    public interface IBusiness<T>
    {
        Task<IEnumerable<T>> GetAsync();

        Task<T> GetAsync(long id);

        Task<bool> DeleteAsync(long id);

        Task<int> AddAsync(IEnumerable<T> entities);

        Task<T> AddAsync(T entity);

        Task<T> UpdateAsync(T entity);
    }
}