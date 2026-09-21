using Common.Helpers;

using DAL.Entity;

using Microsoft.AspNetCore.Mvc;

namespace API.Controller;

public interface ICompositeApiController<T, in TKey1, in TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    Task<ActionResult<ApiResult<T>>> Get(TKey1 key1, TKey2 key2, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Delete(TKey1 key1, TKey2 key2, CancellationToken cancellationToken = default);

    Task<ActionResult<ApiResult<T>>> Post(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all non-deleted composite records.
    /// </summary>
    Task<ActionResult<ApiResult<IEnumerable<T>>>> Get(CancellationToken cancellationToken = default);
}