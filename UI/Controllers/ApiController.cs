using Common.Helpers;

using Microsoft.AspNetCore.Mvc;

using RestSharp;

using UI.Models;
using UI.Services;

namespace UI.Controllers;

[Route("Api")]
public class ApiController : Controller
{
    private readonly IGatewayApiClient gateway;
    private readonly IWebHostEnvironment env;

    public ApiController(IGatewayApiClient gateway, IWebHostEnvironment env)
    {
        this.gateway = gateway;
        this.env = env;
    }

    #region Users
    [HttpGet("Users")]
    public Task<ActionResult> GetUsersAsync(CancellationToken cancellationToken)
    {
        return ForwardAsync(() => gateway.GetAsync("Users", cancellationToken));
    }
    #endregion

    #region Notifications
    [HttpPost("Notifications")]
    [ValidateAntiForgeryToken]
    public Task<ActionResult> PostNotificationsAsync([FromBody] NotificationModel notificationModel, CancellationToken cancellationToken)
    {
        return ForwardAsync(() => gateway.PostAsync("Notifications", notificationModel, cancellationToken));
    }
    #endregion

    #region NotificationHistories
    [HttpGet("NotificationHistories")]
    public Task<ActionResult> GetNotificationHistoriesAsync(CancellationToken cancellationToken)
    {
        return ForwardAsync(() => gateway.GetAsync("NotificationHistories", cancellationToken));
    }

    [HttpPost("NotificationHistories")]
    [ValidateAntiForgeryToken]
    public Task<ActionResult> PostNotificationHistoriesAsync([FromBody] object notificationHistoryModels, CancellationToken cancellationToken)
    {
        return ForwardAsync(() => gateway.PostAsync("Notifications/Send", notificationHistoryModels, cancellationToken));
    }
    #endregion

    // T2: internal for unit testing
    internal async Task<ActionResult> ForwardAsync(Func<Task<RestResponse>> send)
    {
        try
        {
            var response = await send().ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (status < 100)
                return StatusCode(502, new ApiResult<dynamic>(false, error: new ErrorResult(0, response.ErrorMessage ?? "Gateway request failed.")));

            if (string.IsNullOrWhiteSpace(response.Content))
                return StatusCode(status);

            // S4: respect actual content type from the response
            var contentType = response.ContentType ?? "application/json";
            return new ContentResult
            {
                StatusCode = status,
                Content = response.Content,
                ContentType = contentType
            };
        }
        catch (Exception exc)
        {
            return StatusCode(500, new ApiResult<dynamic>(false, error: new ErrorResult(0, exc, env)));
        }
    }
}
