using Common.Helpers;

using DAL.Entity;

using Microsoft.AspNetCore.Mvc;

namespace API.Controller;

public interface IApiController<T, in TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    Task<ActionResult<ApiResult<IEnumerable<T>>>> Get(CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Get(TKey id, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Delete(TKey id, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Post(T entity, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> PostBulk(ICollection<T> entities, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Put(T entity, CancellationToken cancellationToken = default);
}