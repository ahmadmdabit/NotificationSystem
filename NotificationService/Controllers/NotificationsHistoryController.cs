using Microsoft.AspNetCore.Mvc;

using NotificationService.Businesses;
using NotificationService.Entities;

namespace API.Controller;

[Route("api/[controller]")]
public sealed class NotificationHistoriesController
    : BaseCompositeApiController<NotificationHistory, long, long>
{
    public NotificationHistoriesController(
        NotificationHistoryBusiness business,
        ILogger<NotificationHistoriesController> logger)
        : base(business, logger)
    {
    }

    // GET /api/notificationhistories/100/42
    // DELETE /api/notificationhistories/100/42
    // POST /api/notificationhistories
}