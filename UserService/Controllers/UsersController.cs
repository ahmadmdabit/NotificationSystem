using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using API.Controller;

using BLL.Business;

using Common.Helpers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using UserService.Businesses;
using UserService.Entities;
using UserService.Models;

namespace UserService.Controllers;

public class UsersController : BaseApiController<User, long>
{
    private readonly AppSettings appSettings;
    public UsersController(IBusiness<User, long> business, ILogger<BaseApiController<User, long>> logger, IOptions<AppSettings> options) : base(business, logger)
    {
        appSettings = options.Value;
    }

    [HttpPost]
    public Task<ActionResult<ApiResult<User>>> Post([FromBody] User entity)
        => Task.FromResult<ActionResult<ApiResult<User>>>(
            this.BadRequestApi("Use /api/Users/Register to create users."));

    [HttpPost("Bulk")]
    public Task<ActionResult<ApiResult<User>>> PostBulk([FromBody] IEnumerable<User> entities)
        => Task.FromResult<ActionResult<ApiResult<User>>>(
            this.BadRequestApi("Use /api/Users/Register to create users."));

    [HttpPut]
    public Task<ActionResult<ApiResult<User>>> Put([FromBody] User entity)
        => Task.FromResult<ActionResult<ApiResult<User>>>(
            this.BadRequestApi("User updates require a dedicated endpoint (not implemented)."));

    [AllowAnonymous]
    // POST: api/[controller]/Register
    [HttpPost("Register")]
    public async Task<ActionResult<ApiResult<User>>> PostRegister([FromBody] RegisterModel model)
    {
        // null body → 400 instead of NRE 500 (matches PostAuthenticate guard)
        ArgumentNullException.ThrowIfNull(model);

        this.Logger.LogInformation("[PostRegister] [{Ip}] Username={Username}", ClientIp, model.Username);
        var entity = await ((UserBusiness)this.Business).RegisterAsync(model).ConfigureAwait(false);
        if (entity != null)
        {
            entity.PasswordHash = null!;
            entity.PasswordSalt = null!;
            return Ok(new ApiResult<User>(true, entity));
        }
        return this.BadRequestApi();
    }

    [AllowAnonymous]
    // POST: api/[controller]/Authenticate
    [HttpPost("Authenticate")]
    public async Task<ActionResult<ApiResult<User>>> PostAuthenticate([FromBody] AuthenticateModel model)
    {
        this.Logger.LogInformation("[PostAuthenticate] [{Ip}] Username={Username}", ClientIp, model?.Username);
        ArgumentNullException.ThrowIfNull(model);
        var entity = await ((UserBusiness)this.Business).AuthenticateAsync(model).ConfigureAwait(false);
        if (entity != null)
        {
            entity.PasswordHash = null!;
            entity.PasswordSalt = null!;
            entity.Token = TokenGenerate(entity.Id);

            return Ok(new ApiResult<User>(true, entity));
        }
        return this.BadRequestApi();
    }

    private string TokenGenerate(long id)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(appSettings.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, id.ToString())
            ]),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}