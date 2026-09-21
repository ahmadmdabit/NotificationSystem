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
public abstract class BaseApiController<T, TKey> : ControllerBase, IApiController<T, TKey>
    where T : class, IEntity<T, TKey>, new()
    where TKey : notnull
{
    protected readonly IBusiness<T, TKey> Business;
    protected readonly ILogger<BaseApiController<T, TKey>> Logger;

    protected string ClientIp => HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

    protected BaseApiController(IBusiness<T, TKey> business, ILogger<BaseApiController<T, TKey>> logger)
    {
        Business = business ?? throw new ArgumentNullException(nameof(business));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // GET: api/[controller]
    [HttpGet]
    public virtual async Task<ActionResult<ApiResult<IEnumerable<T>>>> Get(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Get] [{Ip}] Entity: {Entity}", ClientIp, typeof(T).Name);
        var result = await Business.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<IEnumerable<T>>(true, result));
    }

    // GET: api/[controller]/{id}
    [HttpGet("{id}")]
    public virtual async Task<ActionResult<ApiResult<T>>> Get(TKey id, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Get:{Id}] [{Ip}] Entity: {Entity}", id, ClientIp, typeof(T).Name);
        try
        {
            var entity = await Business.GetAsync(id, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Ok(new ApiResult<T>(true, entity));
        }
        catch (KeyNotFoundException)
        {
            return NotFoundApi($"Entity '{typeof(T).Name}' with id [{id}] could not be found.");
        }
    }

    // DELETE: api/[controller]/{id}
    [HttpDelete("{id}")]
    public virtual async Task<ActionResult<ApiResult<T>>> Delete(TKey id, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Delete:{Id}] [{Ip}] Entity: {Entity}", id, ClientIp, typeof(T).Name);
        if (await Business.DeleteAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return Ok(new ApiResult<T>(true, null!));
        }
        return NotFoundApi();
    }

    // POST: api/[controller]
    [HttpPost]
    public virtual async Task<ActionResult<ApiResult<T>>> Post([FromBody] T entity, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Post] [{Ip}] Entity: {Entity}", ClientIp, typeof(T).Name);
        var created = await Business.AddAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (created != null)
        {
            return Ok(new ApiResult<T>(true, created));
        }
        return BadRequestApi();
    }

    // POST: api/[controller]/Bulk
    [HttpPost("Bulk")]
    public virtual async Task<ActionResult<ApiResult<T>>> PostBulk([FromBody] ICollection<T> entities, CancellationToken cancellationToken = default)
    {
        var count = entities?.Count ?? 0;
        Logger.LogInformation("[PostBulk] [{Ip}] Entity: {Entity} Count: {Count}", ClientIp, typeof(T).Name, count);

        var affected = await Business.AddAsync(entities!, cancellationToken).ConfigureAwait(false);
        if (affected > 0)
        {
            return Ok(new ApiResult<int>(true, affected));
        }
        return BadRequestApi("No records were inserted.");
    }

    // PUT: api/[controller]
    [HttpPut]
    public virtual async Task<ActionResult<ApiResult<T>>> Put([FromBody] T entity, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("[Put] [{Ip}] Entity: {Entity}", ClientIp, typeof(T).Name);
        var updated = await Business.UpdateAsync(entity, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (updated != null)
        {
            return Ok(new ApiResult<T>(true, updated));
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