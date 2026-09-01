using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    internal Task<bool> CurrentViewerIsAdvertiserAsync(
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.memberships membership
                WHERE membership.tenant_id = commercial.current_tenant_id()
                  AND membership.user_id = commercial.current_user_id()
                  AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
                  AND membership.role_code IN (
                      {MasterDataCodes.Roles.AdvertiserAdmin},
                      {MasterDataCodes.Roles.AdvertiserApprover})) AS "Value"
            """).SingleAsync(cancellationToken);
}
