using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    // Held by the command transaction: ReadCommitted freshness checks must wait for
    // any in-flight confirmation of another shortlist for this exact mix.
    internal Task<Guid> LockSelectionMixAsync(TenantId tenantId, Guid mixId,
        CancellationToken cancellationToken) => DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.media_mix_versions
            WHERE tenant_id = {tenantId.Value} AND id = {mixId}
            FOR UPDATE
            """).SingleAsync(cancellationToken);

    internal Task<bool> HasNewerConfirmedShortlistAsync(TenantId tenantId, ShortlistRow shortlist,
        CancellationToken cancellationToken) => DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.inventory_shortlist_versions newer
                WHERE newer.tenant_id = {tenantId.Value}
                  AND newer.brief_version_id = {shortlist.BriefVersionId}
                  AND newer.mix_version_id = {shortlist.MixVersionId}
                  AND newer.version_no > {shortlist.VersionNumber}
                  AND newer.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            ) AS "Value"
            """).SingleAsync(cancellationToken);

    // Alternative mixes remain independent. A later confirmed choice for the SAME
    // mix supersedes its earlier selection; historical plans remain readable.
    internal Task<bool> HasSupersedingSelectionAsync(
        TenantId tenantId, Guid[] planIds, CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.media_plan_versions plan
                JOIN commercial.inventory_shortlist_versions original
                  ON original.tenant_id = plan.tenant_id AND original.id = plan.shortlist_version_id
                JOIN commercial.inventory_shortlist_versions newer
                  ON newer.tenant_id = plan.tenant_id AND newer.brief_version_id = plan.brief_version_id
                 AND newer.mix_version_id = plan.mix_version_id AND newer.version_no > original.version_no
                WHERE plan.tenant_id = {tenantId.Value} AND plan.id = ANY({planIds})
                  AND newer.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            ) AS "Value"
            """).SingleAsync(cancellationToken);
}
