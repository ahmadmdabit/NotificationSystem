using BLL.Business;
using DAL.Repository;
using NotificationService.Entities;

namespace NotificationService.Businesses
{
    public class NotificationHistoryBusiness : BaseBusiness<NotificationHistory>
    {
        public NotificationHistoryBusiness(IRepository<NotificationHistory> repository) : base(repository)
        {
        }
    }
}