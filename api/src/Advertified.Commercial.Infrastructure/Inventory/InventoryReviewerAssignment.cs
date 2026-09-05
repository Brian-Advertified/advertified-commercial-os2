using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryReviewerAssignment
{
    internal static async Task<Guid?> FindAsync(GovernanceDbContext db, Guid tenantId, Guid creatorId,
        CancellationToken cancellationToken)
    {
        var reviewers = await db.Database.SqlQuery<Guid>($"""
            SELECT membership.user_id AS "Value"
            FROM commercial.memberships membership
            WHERE membership.tenant_id = {tenantId} AND membership.user_id <> {creatorId}
              AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND membership.role_code = ANY({InventoryReviewerRoles.Inventory})
            ORDER BY membership.role_code, membership.user_id LIMIT 1
            """).ToListAsync(cancellationToken);
        return reviewers.Count == 1 ? reviewers[0] : null;
    }
}
