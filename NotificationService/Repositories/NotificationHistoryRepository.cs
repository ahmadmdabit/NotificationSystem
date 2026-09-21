using System.Data;

using DAL.Repository;

using NotificationService.Entities;

namespace NotificationService.Repositories;

public class NotificationHistoryRepository : BaseCompositeRepository<NotificationHistory, long, long>
{
    public NotificationHistoryRepository(IDbConnection dbConnection) : base(dbConnection)
    {
    }
}