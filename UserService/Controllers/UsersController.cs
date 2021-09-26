using API.Controller;
using BLL.Business;
using Common.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UserService.Businesses;
using UserService.Entities;
using UserService.Models;

namespace UserService.Controllers
{
    public class UsersController : BaseApiController<User>
    {
        private readonly AppSettings _appSettings;
        public UsersController(IBusiness<User> business, ILogger<BaseApiController<User>> logger, IActionContextAccessor accessor, IOptions<AppSettings> options) : base(business, logger, accessor)
        {
            _appSettings = options.Value;
        }

        [AllowAnonymous]
        // POST: api/[controller]/Register
        [HttpPost("Register")]
        public async Task<ActionResult<ApiResult<User>>> PostRegister([FromBody] RegisterModel model)
        {
            this._logger.LogInformation($"[PostRegister] [{this._ip}] {JsonConvert.SerializeObject(model)}");
            var entity = await (this._business as UserBusiness).RegisterAsync(model).ConfigureAwait(false);
            if (entity != null)
            {
                return Ok(new ApiResult<User>(true, null));
            }
            return this.BadRequestApi();
        }

        [AllowAnonymous]
        // POST: api/[controller]/Authenticate
        [HttpPost("Authenticate")]
        public async Task<ActionResult<ApiResult<User>>> PostAuthenticate([FromBody] AuthenticateModel model)
        {
            this._logger.LogInformation($"[PostAuthenticate] [{this._ip}] {JsonConvert.SerializeObject(model)}");
            var entity = await (this._business as UserBusiness).AuthenticateAsync(model).ConfigureAwait(false);
            if (entity != null)
            {
                entity.PasswordHash = null;
                entity.PasswordSalt = null;
                entity.Token = TokenGenerate(entity.Id);

                return Ok(new ApiResult<User>(true, entity));
            }
            return this.BadRequestApi();
        }

        private string TokenGenerate(long id)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, id.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}