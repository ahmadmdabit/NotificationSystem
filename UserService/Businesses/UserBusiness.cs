using BLL.Business;
using DAL.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UserService.Entities;
using UserService.Models;

namespace UserService.Businesses
{
    public class UserBusiness : BaseBusiness<User>
    {
        public UserBusiness(IRepository<User> repository) : base(repository)
        {
        }

        public override async Task<IEnumerable<User>> GetAsync()
        {
            return await this._repository.GetAsync("Id, Username, CreatedAt, UpdatedAt, IsDeleted").ConfigureAwait(false);
        }

        public override async Task<User> GetAsync(long id)
        {
            return await this._repository.GetAsync(id, "Id, Username, CreatedAt, UpdatedAt, IsDeleted").ConfigureAwait(false);
        }

        public async Task<User> RegisterAsync(RegisterModel model)
        {
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                return null;
            var entities = (await this._repository.GetAsync("Username", model.Username).ConfigureAwait(false))?.ToList();
            if (entities?.Count > 0)
            {
                return null;
            }
            byte[] passwordHash, passwordSalt;
            CreatePasswordHash(model.Password, out passwordHash, out passwordSalt);
            return await this._repository.InsertAsync(new User { Username = model.Username, PasswordHash = passwordHash, PasswordSalt = passwordSalt }).ConfigureAwait(false);
        }

        public async Task<User> AuthenticateAsync(AuthenticateModel model)
        {
            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                return null;

            var entities = (await this._repository.GetAsync("Username", model.Username).ConfigureAwait(false))?.ToList();
            if (entities?.Count > 0)
            {
                if (VerifyPasswordHash(model.Password, entities[0].PasswordHash, entities[0].PasswordSalt))
                    return entities[0];
            }
            return null;
        }

        private static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            if (password == null) throw new ArgumentNullException("password");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Value cannot be empty or whitespace only string.", "password");

            using (var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private static bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            if (password == null) throw new ArgumentNullException("password");
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Value cannot be empty or whitespace only string.", "password");
            if (storedHash.Length != 64) throw new ArgumentException("Invalid length of password hash (64 bytes expected).", "passwordHash");
            if (storedSalt.Length != 128) throw new ArgumentException("Invalid length of password salt (128 bytes expected).", "passwordHash");

            using (var hmac = new System.Security.Cryptography.HMACSHA512(storedSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != storedHash[i]) return false;
                }
            }

            return true;
        }
    }
}