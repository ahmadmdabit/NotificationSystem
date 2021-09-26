using DAL.Repository;
using System.Data;
using UserService.Entities;

namespace UserService.Repositories
{
    public class UserRepository : BaseRepository<User>
    {
        public UserRepository(IDbConnection dbConnection) : base(dbConnection)
        {
        }
    }
}