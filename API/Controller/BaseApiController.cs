using BLL.Business;
using Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controller
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseApiController<T> : ControllerBase, IApiController<T> where T : class
    {
        protected readonly IBusiness<T> _business;
        protected readonly ILogger<BaseApiController<T>> _logger;
        protected readonly IActionContextAccessor _accessor;
        protected readonly string _ip;

        protected BaseApiController(IBusiness<T> business, ILogger<BaseApiController<T>> logger, IActionContextAccessor accessor)
        {
            this._business = business;
            this._logger = logger;
            this._accessor = accessor;
            this._ip = this._accessor.ActionContext.HttpContext.Connection.RemoteIpAddress.ToString();
        }

        // GET: api/[controller]
        [HttpGet]
        public virtual async Task<ActionResult<ApiResult<IEnumerable<T>>>> Get()
        {
            this._logger.LogInformation($"[Get] [{this._ip}]");
            return Ok(new ApiResult<IEnumerable<T>>(true, await this._business.GetAsync().ConfigureAwait(false)));
        }

        // GET: api/[controller]/5
        [HttpGet("{id}")]
        public virtual async Task<ActionResult<ApiResult<T>>> Get(long id)
        {
            this._logger.LogInformation($"[Get:{id}] [{this._ip}]");
            var entity = await this._business.GetAsync(id).ConfigureAwait(false);
            if (entity != null)
            {
                return Ok(new ApiResult<T>(true, entity));
            }
            return this.NotFoundApi();
        }

        // DELETE: api/[controller]/5
        [HttpDelete("{id}")]
        public virtual async Task<ActionResult<ApiResult<T>>> Delete(long id)
        {
            this._logger.LogInformation($"[Delete:{id}] [{this._ip}]");
            if (await this._business.DeleteAsync(id).ConfigureAwait(false))
            {
                return Ok(new ApiResult<T>(true, null));
            }
            return this.NotFoundApi();
        }

        // POST: api/[controller]
        [HttpPost]
        public virtual async Task<ActionResult<ApiResult<T>>> Post([FromBody] T entity)
        {
            this._logger.LogInformation($"[Post] [{this._ip}] {JsonConvert.SerializeObject(entity)}");
            entity = await this._business.AddAsync(entity).ConfigureAwait(false);
            if (entity != null)
            {
                return Ok(new ApiResult<T>(true, entity));
            }
            return this.BadRequestApi();
        }

        // POST: api/[controller]/Bulk
        [HttpPost("Bulk")]
        public virtual async Task<ActionResult<ApiResult<T>>> PostBulk([FromBody] IEnumerable<T> entities)
        {
            this._logger.LogInformation($"[PostBulk] [{this._ip}] {JsonConvert.SerializeObject(entities)}");
            var added = await this._business.AddAsync(entities).ConfigureAwait(false);
            if (added > 0)
            {
                return Ok(new ApiResult<T>(true, null));
            }
            return this.BadRequestApi();
        }

        // PUT: api/[controller]
        [HttpPut]
        public virtual async Task<ActionResult<ApiResult<T>>> Put([FromBody] T entity)
        {
            this._logger.LogInformation($"[Put] [{this._ip}] {JsonConvert.SerializeObject(entity)}");
            entity = await this._business.UpdateAsync(entity).ConfigureAwait(false);
            if (entity != null)
            {
                return Ok(new ApiResult<T>(true, entity));
            }
            return this.BadRequestApi();
        }

        protected virtual ActionResult<ApiResult<T>> NotFoundApi(string message = null)
        {
            this._logger.LogInformation(message ?? "NotFound");
            return Ok(new ApiResult<T>(false, null, new NotFoundResult().StatusCode, message ?? "NotFound"));
        }

        protected virtual ActionResult<ApiResult<T>> BadRequestApi(string message = null)
        {
            this._logger.LogInformation(message ?? "BadRequest");
            return Ok(new ApiResult<T>(false, null, new BadRequestResult().StatusCode, message ?? "BadRequest"));
        }
    }
}