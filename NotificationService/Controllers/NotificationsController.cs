using API.Controller;

using BLL.Business;

using Common.Helpers;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;

using NotificationService.Businesses;
using NotificationService.Entities;

namespace NotificationService.Controllers;

public class NotificationsController : BaseApiController<Notification, long>
{
    public NotificationsController(IBusiness<Notification, long> business, ILogger<BaseApiController<Notification, long>> logger) : base(business, logger)
    {
    }

    // POST: api/[controller]/Send
    [HttpPost("Send")]
    public async Task<ActionResult<ApiResult<Notification>>> PostSend([FromBody] IEnumerable<NotificationHistory> entities, CancellationToken cancellationToken = default)
    {
        this.Logger.LogInformation("[PostSend] [{Ip}] {Entities}", ClientIp, JsonConvert.SerializeObject(entities));
        var spResult = await ((NotificationBusiness)this.Business).SendAsync(entities, cancellationToken).ConfigureAwait(false);
        if (spResult.Success)
        {
            return Ok(new ApiResult<Notification>(true, null));
        }
        return this.BadRequestApi(spResult.Message);
    }
}