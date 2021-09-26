using API.Controller;
using BLL.Business;
using Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NotificationService.Businesses;
using NotificationService.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NotificationService.Controllers
{
    public class NotificationsController : BaseApiController<Notification>
    {
        public NotificationsController(IBusiness<Notification> business, ILogger<BaseApiController<Notification>> logger, IActionContextAccessor accessor) : base(business, logger, accessor)
        {
        }

        // POST: api/[controller]/Send
        [HttpPost("Send")]
        public async Task<ActionResult<ApiResult<Notification>>> PostSend([FromBody] IEnumerable<NotificationHistory> entities)
        {
            this._logger.LogInformation($"[PostSend] [{this._ip}] {JsonConvert.SerializeObject(entities)}");
            var spResult = await (this._business as NotificationBusiness).SendAsync(entities).ConfigureAwait(false);
            if (spResult.Success)
            {
                return Ok(new ApiResult<Notification>(true, null));
            }
            return this.BadRequestApi(spResult.Message);
        }
    }
}