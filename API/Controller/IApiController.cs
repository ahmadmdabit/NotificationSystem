using Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controller
{
    public interface IApiController<T>
    {
        Task<ActionResult<ApiResult<IEnumerable<T>>>> Get();

        Task<ActionResult<ApiResult<T>>> Get(long id);

        Task<ActionResult<ApiResult<T>>> Delete(long id);

        Task<ActionResult<ApiResult<T>>> Post(T entity);

        Task<ActionResult<ApiResult<T>>> PostBulk(IEnumerable<T> entities);

        Task<ActionResult<ApiResult<T>>> Put(T entity);
    }
}