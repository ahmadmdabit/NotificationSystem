using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using NotificationService.Application.Commands.CreateNotification;
using NotificationService.Application.Commands.DeleteNotification;
using NotificationService.Application.Commands.DeleteNotificationHistory;
using NotificationService.Application.Commands.SendNotifications;
using NotificationService.Application.Commands.UpdateNotification;
using NotificationService.Application.DTOs;
using NotificationService.Application.Queries.GetAllNotifications;
using NotificationService.Application.Queries.GetNotificationById;
using NotificationService.Application.Queries.GetNotificationHistory;
using NotificationService.Application.Queries.GetNotificationHistoryById;

using Shared.Helpers;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator mediator;

    public NotificationsController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResult<IReadOnlyList<NotificationDto>>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllNotificationsQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<IReadOnlyList<NotificationDto>>(true, result));
    }

    // Explicit route name: SuppressAsyncSuffixInActionNames defaults to true (ASP.NET Core
    // 3.0+), so the action selector is "GetById", not "GetByIdAsync". CreatedAtAction resolves
    // by action name and therefore could not find it, failing result *formatting* with
    // "No route matches the supplied values" and turning a successful create into a 500.
    // Naming the route and using CreatedAtRoute makes the reference explicit and stable
    // regardless of the suffix convention (N-04, dispatch-buffer Phase 1).
    [HttpGet("{id:long}", Name = nameof(GetByIdAsync))]
    [Authorize]
    public async Task<ActionResult<ApiResult<NotificationDto>>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNotificationByIdQuery(id), cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new ApiResult<NotificationDto>(false, default, 404, "Notification not found."));

        return Ok(new ApiResult<NotificationDto>(true, result));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResult<NotificationDto>>> CreateAsync(
        [FromBody] CreateNotificationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return CreatedAtRoute(nameof(GetByIdAsync), new { id = result.Id }, new ApiResult<NotificationDto>(true, result));
    }

    [HttpPut("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResult<NotificationDto>>> UpdateAsync(
        long id,
        [FromBody] UpdateNotificationCommand command,
        CancellationToken cancellationToken)
    {
        command.Id = id;

        // Handler returns null for unknown ids; UpdateAsync asserts rows-affected for the
        // concurrent-delete race (NotFoundException → 404 via ApiExceptionHandler).
        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new ApiResult<NotificationDto>(false, default, 404, "Notification not found."));

        return Ok(new ApiResult<NotificationDto>(true, result));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Service")]
    public async Task<ActionResult<ApiResult<bool>>> DeleteAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteNotificationCommand(id), cancellationToken).ConfigureAwait(false);
        if (!result)
            return NotFound(new ApiResult<bool>(false, default, 404, "Notification not found."));

        return Ok(new ApiResult<bool>(true, true));
    }

    [HttpPost("Send")]
    [Authorize]
    public async Task<ActionResult<ApiResult<bool>>> SendAsync(
        [FromBody] List<SendNotificationItem> items,
        CancellationToken cancellationToken)
    {
        // The handler returns no value: it either completes, or throws a typed exception that
        // ApiExceptionHandler maps to 404/400/500. There is no failure outcome to branch on here,
        // so a dead else-BadRequest would only ever be unreachable (N-04).
        await mediator.Send(new SendNotificationsCommand { Items = items }, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ApiResult<bool>(true, true));
    }
}

[ApiController]
[Route("api/[controller]")]
public class NotificationHistoriesController : ControllerBase
{
    private readonly IMediator mediator;

    public NotificationHistoriesController(IMediator mediator)
    {
        this.mediator = mediator;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResult<IReadOnlyList<NotificationHistoryDto>>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNotificationHistoryQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<IReadOnlyList<NotificationHistoryDto>>(true, result));
    }

    [HttpGet("{key1:long}/{key2:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResult<NotificationHistoryDto>>> GetByIdAsync(
        long key1,
        long key2,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNotificationHistoryByIdQuery(key1, key2), cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new ApiResult<NotificationHistoryDto>(false, default, 404, "Notification history not found."));

        return Ok(new ApiResult<NotificationHistoryDto>(true, result));
    }

    [HttpDelete("{key1:long}/{key2:long}")]
    [Authorize(Policy = "Service")]
    public async Task<ActionResult<ApiResult<bool>>> DeleteAsync(
        long key1,
        long key2,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteNotificationHistoryCommand(key1, key2), cancellationToken).ConfigureAwait(false);
        if (!result)
            return NotFound(new ApiResult<bool>(false, default, 404, "Notification history not found."));

        return Ok(new ApiResult<bool>(true, true));
    }
}