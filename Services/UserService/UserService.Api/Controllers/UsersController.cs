using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Helpers;
using UserService.Application.Commands.AuthenticateUser;
using UserService.Application.Commands.DeleteUser;
using UserService.Application.Commands.RegisterUser;
using UserService.Application.DTOs;
using UserService.Application.Queries.GetAllUsers;
using UserService.Application.Queries.GetUserById;
using UserService.Application.Queries.GetUserByUsername;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("Register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<UserDto>>> RegisterAsync(
        [FromBody] RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        // DuplicateEntityException (pre-check + UXUsersUsername race) and ValidationException
        // are translated to 400/ApiResult by ApiExceptionHandler; no local catch so the
        // single translation point owns the envelope.
        var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<UserDto>(true, result));
    }

    [HttpPost("Authenticate")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<AuthenticateResultDto>>> AuthenticateAsync(
        [FromBody] AuthenticateUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return BadRequest(new ApiResult<AuthenticateResultDto>(false, default, 0, "Invalid credentials."));

        return Ok(new ApiResult<AuthenticateResultDto>(true, result));
    }

    [HttpGet]
    [Authorize(Policy = "Service")]
    public async Task<ActionResult<ApiResult<IReadOnlyList<UserReadDto>>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllUsersQuery(), cancellationToken).ConfigureAwait(false);
        return Ok(new ApiResult<IReadOnlyList<UserReadDto>>(true, result));
    }

    [HttpGet("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResult<UserReadDto>>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(id), cancellationToken).ConfigureAwait(false);
        if (result is null)
            return NotFound(new ApiResult<UserReadDto>(false, default, 404, "User not found."));

        return Ok(new ApiResult<UserReadDto>(true, result));
    }

    [HttpGet("username/{username}")]
    [Authorize]
    public async Task<ActionResult<ApiResult<UserReadDto>>> GetByUsernameAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByUsernameQuery(username), cancellationToken).ConfigureAwait(false);
        if (result is null)
            // Generic message: echoing the identifier confirms account existence.
            return NotFound(new ApiResult<UserReadDto>(false, default, 404, "User not found."));

        return Ok(new ApiResult<UserReadDto>(true, result));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Service")]
    public async Task<ActionResult<ApiResult<bool>>> DeleteAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id), cancellationToken).ConfigureAwait(false);
        if (!result)
            return NotFound(new ApiResult<bool>(false, default, 404, "User not found."));

        return Ok(new ApiResult<bool>(true, true));
    }
}
