using System.Data;

using BLL.Business;

using Common.Helpers;

using DAL.Data.Tvp;
using DAL.Repository;

using Dapper;

using NotificationService.Data.Tvp;
using NotificationService.Entities;

namespace NotificationService.Businesses;

public class NotificationBusiness : BaseBusiness<Notification, long>
{
    public NotificationBusiness(IRepository<Notification, long> repository) : base(repository)
    {
    }

    public async Task<SpResult> SendAsync(IEnumerable<NotificationHistory> entities, CancellationToken cancellationToken = default)
    {
        var tvpDefinition = NotificationHistoryTvpDefinition.Instance;

        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Entities",
            value: entities.AsSqlDataRecords(tvpDefinition)
                           .AsTableValuedParameter(tvpDefinition.TypeName)
        );
        parameters.Add("@SPSuccess", value: null, dbType: DbType.Boolean, direction: ParameterDirection.Output);
        parameters.Add("@SPMessage", value: null, dbType: DbType.String, direction: ParameterDirection.Output, size: 255);

        return await this.Repository.QueryAsync(
            "[dbo].[SPNotificationHistoryInsert]",
            parameters,
            CommandType.StoredProcedure,
            cancellationToken
        ).ConfigureAwait(false);
    }
}