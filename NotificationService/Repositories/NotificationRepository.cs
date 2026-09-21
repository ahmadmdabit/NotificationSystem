using System.Data;

using DAL.Repository;

using NotificationService.Entities;

namespace NotificationService.Repositories;

public class NotificationRepository : BaseRepository<Notification, long>
{
    public NotificationRepository(IDbConnection dbConnection) : base(dbConnection)
    {
    }
}