using System.Data;

using DAL.Repository;

using UserService.Entities;

namespace UserService.Repositories;

public class UserRepository : BaseRepository<User, long>
{
    public UserRepository(IDbConnection dbConnection) : base(dbConnection)
    {
    }
}