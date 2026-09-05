using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalRecordStore
{
    internal Task<List<ProposalInventoryImpactView>> ListInventoryImpactsAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ProposalInventoryImpactView>($"""
            SELECT impact.id AS "Id",
                impact.proposal_version_id AS "ProposalVersionId",
                impact.proposal_option_id AS "ProposalOptionId",
                impact.media_plan_line_id AS "MediaPlanLineId",
                impact.inventory_tenant_id AS "InventoryTenantId",
                impact.supplier_id AS "SupplierId",
                impact.old_release_id AS "OldReleaseId",
                impact.replacement_release_id AS "ReplacementReleaseId",
                impact.old_product_id AS "OldProductId",
                impact.old_product_version_id AS "OldProductVersionId",
                impact.old_rate_id AS "OldRateId",
                impact.old_availability_id AS "OldAvailabilityId",
                impact.replacement_product_id AS "ReplacementProductId",
                impact.replacement_product_version_id AS "ReplacementProductVersionId",
                impact.replacement_rate_id AS "ReplacementRateId",
                impact.replacement_availability_id AS "ReplacementAvailabilityId",
                impact.impact_type_code AS "ImpactType",
                impact.status_code AS "Status",
                impact.comparison_json::text AS "ComparisonJson",
                impact.resolved_by AS "ResolvedBy",
                impact.resolved_at_utc AS "ResolvedAtUtc",
                impact.version AS "Version"
            FROM commercial.proposal_inventory_impacts impact
            WHERE impact.tenant_id = {tenantId.Value}
              AND impact.proposal_version_id = {proposalVersionId}
            ORDER BY impact.detected_at_utc, impact.id
            """).ToListAsync(cancellationToken);
}
