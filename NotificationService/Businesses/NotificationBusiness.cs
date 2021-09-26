using BLL.Business;
using Common.Helpers;
using Common.Extensions;
using DAL.Repository;
using Dapper;
using NotificationService.Entities;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using System;

namespace NotificationService.Businesses
{
    public class NotificationBusiness : BaseBusiness<Notification>
    {
        public NotificationBusiness(IRepository<Notification> repository) : base(repository)
        {
        }

        public async Task<SpResult> SendAsync(IEnumerable<NotificationHistory> entities)
        {
            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("@Entities",
                entities.ToDataTable(new Dictionary<string, Type>
                {
                    { "NotificationId", typeof(long) },
                    { "UserId", typeof(long) }
                }).AsTableValuedParameter("[dbo].[Type_NotificationHistory]"));
            parameters.Add("@SP_Success", value: null, dbType: DbType.Boolean, direction: ParameterDirection.Output);
            parameters.Add("@SP_Message", value: null, dbType: DbType.String, direction: ParameterDirection.Output, size: 255);
            return await this._repository.QueryAsync("[dbo].[SP_NotificationHistory_I]", parameters, CommandType.StoredProcedure).ConfigureAwait(false);
        }
    }
}