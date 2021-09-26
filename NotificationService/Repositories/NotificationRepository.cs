using DAL.Repository;
using NotificationService.Entities;
using System.Data;

namespace NotificationService.Repositories
{
    public class NotificationRepository : BaseRepository<Notification>
    {
        public NotificationRepository(IDbConnection dbConnection) : base(dbConnection)
        {
        }
    }
}