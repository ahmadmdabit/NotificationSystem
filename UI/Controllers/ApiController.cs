using Common.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UI.Models;

namespace UI.Controllers
{
    [Route("Api")]
    public class ApiController : Controller
    {
        private readonly RestClient _RestClient = new RestClient("https://localhost:44315");

        #region Users
        [HttpGet("Users")]
        public async Task<ActionResult> GetUsersAsync()
        {
            try
            {
                return Ok(await _RestClient.GetAsync<dynamic>(new RestRequest("Users", DataFormat.Json), CancellationToken.None));
            }
            catch (Exception exc)
            {

                return Ok(new ApiResult<dynamic>(false, error: new ErrorResult(0, exc)));
            }
        } 
        #endregion

        #region Notifications
        [HttpPost("Notifications")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> PostNotificationsAsync([FromBody] NotificationModel notificationModel)
        {
            try
            {
                return Ok(await _RestClient.PostAsync<dynamic>(new RestRequest("Notifications", DataFormat.Json).AddJsonBody(notificationModel), CancellationToken.None));
            }
            catch (Exception exc)
            {

                return Ok(new ApiResult<dynamic>(false, error: new ErrorResult(0, exc)));
            }
        } 
        #endregion

        #region NotificationHistories
        [HttpGet("NotificationHistories")]
        public async Task<ActionResult> GetNotificationHistoriesAsync()
        {
            try
            {
                return Ok(await _RestClient.GetAsync<dynamic>(new RestRequest("NotificationHistories", DataFormat.Json), CancellationToken.None));
            }
            catch (Exception exc)
            {

                return Ok(new ApiResult<dynamic>(false, error: new ErrorResult(0, exc)));
            }
        }

        [HttpPost("NotificationHistories")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> PostNotificationHistoriesAsync([FromBody] object notificationHistoryModels)
        {
            try
            {
                var models = JsonConvert.DeserializeObject<IEnumerable<NotificationHistoryModel>>(notificationHistoryModels.ToString());
                if (models == null || !models.Any())
                {
                    return Ok(new ApiResult<dynamic>(false, error: new ErrorResult(0, "Null argument!")));

                }
                return Ok(await _RestClient.PostAsync<dynamic>(new RestRequest("Notifications/Send", DataFormat.Json).AddJsonBody(models), CancellationToken.None));
            }
            catch (Exception exc)
            {

                return Ok(new ApiResult<dynamic>(false, error: new ErrorResult(0, exc)));
            }
        } 
        #endregion
    }
}
