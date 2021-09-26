using API.Controller;
using BLL.Business;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using NotificationService.Entities;

namespace NotificationService.Controllers
{
    public class NotificationHistoriesController : BaseApiController<NotificationHistory>
    {
        public NotificationHistoriesController(IBusiness<NotificationHistory> business, ILogger<BaseApiController<NotificationHistory>> logger, IActionContextAccessor accessor) : base(business, logger, accessor)
        {
        }
    }
}