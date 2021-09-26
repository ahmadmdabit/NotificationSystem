using DAL.Repository;
using NotificationService.Entities;
using System.Data;

namespace NotificationService.Repositories
{
    public class NotificationHistoryRepository : BaseRepository<NotificationHistory>
    {
        public NotificationHistoryRepository(IDbConnection dbConnection) : base(dbConnection)
        {
        }
    }
}