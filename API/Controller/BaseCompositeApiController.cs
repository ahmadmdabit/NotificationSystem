using BLL.Business;

using Common.Helpers;

using DAL.Entity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace API.Controller;

[Produces("application/json")]
[Authorize]
[Route("api/[controller]")]
[ApiController]
public abstract class BaseCompositeApiController<T, TKey1, TKey2> : ControllerBase, ICompositeApiController<T, TKey1, TKey2>
    where T : class, ICompositeEntity<T, TKey1, TKey2>, new()
    where TKey1 : notnull
    where TKey2 : notnull
{
    protected readonly ICompositeBusiness<T, TKey1, TKey2> Business;
    protected readonly ILogger<BaseCompositeApiController<T, TKey1, TKey2>> Logger;

    protected string ClientIp => HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

    protected BaseCompositeApiController(
        ICompositeBusiness<T, TKey1, TKey2> business,
        ILogger<BaseCompositeApiController<T, TKey1, TKey2>> logger)
    {
        Business = business ?? throw new ArgumentNullException(nameof(business));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists all records — restores the GET /api/[controller] surface the
    /// UI history grid and Ocelot list route depend on.
    /// </summary>
    // GET: api/[controller]
    [HttpGet]
    public virtual async Task<ActionResult<ApiResult<IEnumerable<T>>>> Get(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Get] [{Ip}] Entity: {Entity}", ClientIp, typeof(T).Name);
        var result = await Business.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<IEnumerable<T>>(true, result));
    }

    // GET: api/[controller]/{key1}/{key2}
    [HttpGet("{key1}/{key2}")]
    public virtual async Task<ActionResult<ApiResult<T>>> Get(
        TKey1 key1,
        TKey2 key2,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Get:{Key1}:{Key2}] [{Ip}] Entity: {Entity}", key1, key2, ClientIp, typeof(T).Name);
        var entity = await Business.GetAsync(key1, key2, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (entity != null)
        {
            return Ok(new ApiResult<T>(true, entity));
        }
        return NotFoundApi($"Record with keys [{key1}, {key2}] could not be found.");
    }

    // DELETE: api/[controller]/{key1}/{key2}
    [HttpDelete("{key1}/{key2}")]
    public virtual async Task<ActionResult<ApiResult<T>>> Delete(
        TKey1 key1,
        TKey2 key2,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Delete:{Key1}:{Key2}] [{Ip}] Entity: {Entity}", key1, key2, ClientIp, typeof(T).Name);
        if (await Business.DeleteAsync(key1, key2, cancellationToken).ConfigureAwait(false))
        {
            return Ok(new ApiResult<T>(true, null!));
        }
        return NotFoundApi();
    }

    // POST: api/[controller]
    [HttpPost]
    public virtual async Task<ActionResult<ApiResult<T>>> Post(
        [FromBody] T entity,
        CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Post] [{Ip}] Entity: {Entity}", ClientIp, typeof(T).Name);
        var created = await Business.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        if (created != null)
        {
            return Ok(new ApiResult<T>(true, created));
        }
        return BadRequestApi();
    }

    protected virtual ActionResult<ApiResult<T>> NotFoundApi(string? message = null)
    {
        Logger.LogWarning("[NotFound] [{Ip}] Message: {Message}", ClientIp, message ?? "NotFound");
        return NotFound(new ApiResult<T>(false, null!, 404, message ?? "NotFound"));
    }

    protected virtual ActionResult<ApiResult<T>> BadRequestApi(string? message = null)
    {
        Logger.LogWarning("[BadRequest] [{Ip}] Message: {Message}", ClientIp, message ?? "BadRequest");
        return BadRequest(new ApiResult<T>(false, null!, 400, message ?? "BadRequest"));
    }
}