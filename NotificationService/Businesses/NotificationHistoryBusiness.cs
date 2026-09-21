using BLL.Business;

using DAL.Repository;

using NotificationService.Entities;

namespace NotificationService.Businesses;

public sealed class NotificationHistoryBusiness
    : BaseCompositeBusiness<NotificationHistory, long, long>
{
    public NotificationHistoryBusiness(ICompositeRepository<NotificationHistory, long, long> repository)
        : base(repository)
    {
    }
}