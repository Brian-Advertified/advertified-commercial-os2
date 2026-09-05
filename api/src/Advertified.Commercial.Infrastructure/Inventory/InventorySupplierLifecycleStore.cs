using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySupplierLifecycleStore(InventoryRecordStore inventoryStore)
{
    internal InventoryRecordStore InventoryStore => inventoryStore;

    internal Task<InventorySupplierLifecycleRow?> FindSupplierAsync(
        TenantId tenantId,
        Guid supplierId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
        var query = FormattableStringFactory.Create(
            SupplierSelect + " WHERE supplier.tenant_id = {0} AND supplier.id = {1}" +
            locking, tenantId.Value, supplierId,
            MasterDataCodes.LifecycleStatuses.Active, MasterDataCodes.LifecycleStatuses.Expired);
        return inventoryStore.DbContext.Database
            .SqlQuery<InventorySupplierLifecycleRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<SupplierClaimInvitationRow?> FindInvitationAsync(
        TenantId tenantId,
        Guid invitationId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
        var query = FormattableStringFactory.Create(
            InvitationSelect +
            " WHERE invitation.tenant_id = {0} AND invitation.id = {1}" + locking,
            tenantId.Value, invitationId);
        return inventoryStore.DbContext.Database
            .SqlQuery<SupplierClaimInvitationRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<SupplierClaimInvitationRow>> ListInvitationsAsync(
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var query = FormattableStringFactory.Create(
            InvitationSelect +
            " WHERE invitation.tenant_id = {0} AND invitation.supplier_id = {1}" +
            " ORDER BY invitation.created_at_utc DESC, invitation.id",
            tenantId.Value, supplierId);
        return inventoryStore.DbContext.Database
            .SqlQuery<SupplierClaimInvitationRow>(query)
            .ToListAsync(cancellationToken);
    }

    internal Task<List<InventorySupplierReleaseRow>> ListReleasesAsync(
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken) =>
        inventoryStore.DbContext.Database
            .SqlQuery<InventorySupplierReleaseRow>($"""
                SELECT release.id AS "Id", release.supplier_id AS "SupplierId",
                    release.source_import_id AS "SourceImportId",
                    release.version_number AS "VersionNumber",
                    release.replacement_mode_code AS "ReplacementMode",
                    release.status_code AS "Status",
                    release.supersedes_release_id AS "SupersedesReleaseId",
                    release.effective_at_utc AS "EffectiveAtUtc",
                    release.superseded_at_utc AS "SupersededAtUtc",
                    count(DISTINCT version.product_id)::integer AS "ProductCount",
                    release.version AS "Version"
                FROM commercial.inventory_supplier_releases release
                LEFT JOIN commercial.inventory_product_versions version
                  ON version.tenant_id = release.tenant_id
                 AND version.inventory_release_id = release.id
                WHERE release.tenant_id = {tenantId.Value}
                  AND release.supplier_id = {supplierId}
                GROUP BY release.id
                ORDER BY release.version_number DESC
                """)
            .ToListAsync(cancellationToken);

    internal Task<ProposalInventoryImpactRow?> FindImpactAsync(
        TenantId tenantId,
        Guid impactId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
        var query = FormattableStringFactory.Create(
            ImpactSelect + " WHERE impact.tenant_id = {0} AND impact.id = {1}" + locking,
            tenantId.Value, impactId);
        return inventoryStore.DbContext.Database
            .SqlQuery<ProposalInventoryImpactRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<ProposalInventoryImpactRow>> ListImpactsAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken)
    {
        var query = FormattableStringFactory.Create(
            ImpactSelect +
            " WHERE impact.tenant_id = {0} AND impact.proposal_version_id = {1}" +
            " ORDER BY impact.detected_at_utc, impact.id",
            tenantId.Value, proposalVersionId);
        return inventoryStore.DbContext.Database
            .SqlQuery<ProposalInventoryImpactRow>(query)
            .ToListAsync(cancellationToken);
    }

    internal async Task<InventorySupplierLifecycleView> BuildSupplierViewAsync(
        TenantId tenantId,
        InventorySupplierLifecycleRow supplier,
        CancellationToken cancellationToken,
        bool includeInvitations = false)
    {
        var releases = await ListReleasesAsync(
            tenantId, supplier.Id, cancellationToken);
        var invitations = includeInvitations
            ? await ListInvitationsAsync(tenantId, supplier.Id, cancellationToken)
            : [];
        return new InventorySupplierLifecycleView(
            supplier.Id, supplier.Name, supplier.ClaimStatus,
            supplier.CurrentReleaseId, supplier.CurrentProductCount,
            supplier.ExpiredProductCount,
            releases.Select(item => item.ToView()).ToArray(),
            invitations.Select(item => item.ToView()).ToArray(), supplier.Version);
    }

    private const string SupplierSelect = """
        SELECT supplier.id AS "Id", supplier.name AS "Name",
            supplier.claim_status_code AS "ClaimStatus",
            supplier.current_inventory_release_id AS "CurrentReleaseId",
            (SELECT count(*)::integer
             FROM commercial.inventory_products product
             WHERE product.tenant_id = supplier.tenant_id
               AND product.supplier_id = supplier.id
               AND product.status_code = {2}) AS "CurrentProductCount",
            (SELECT count(*)::integer
             FROM commercial.inventory_products product
             WHERE product.tenant_id = supplier.tenant_id
               AND product.supplier_id = supplier.id
               AND product.status_code = {3}) AS "ExpiredProductCount",
            supplier.version AS "Version"
        FROM commercial.inventory_suppliers supplier
        """;

    private const string InvitationSelect = """
        SELECT invitation.id AS "Id", invitation.supplier_id AS "SupplierId",
            supplier.name AS "SupplierName",
            invitation.invited_email AS "InvitedEmail",
            invitation.invited_role_code AS "Role",
            invitation.token_hash AS "TokenHash",
            invitation.status_code AS "Status",
            invitation.expires_at_utc AS "ExpiresAtUtc",
            invitation.created_by AS "CreatedBy",
            invitation.created_at_utc AS "CreatedAtUtc",
            invitation.revoked_by AS "RevokedBy",
            invitation.revoked_at_utc AS "RevokedAtUtc",
            invitation.revocation_reason AS "RevocationReason",
            invitation.accepted_user_id AS "AcceptedUserId",
            invitation.accepted_at_utc AS "AcceptedAtUtc",
            invitation.version AS "Version"
        FROM commercial.supplier_claim_invitations invitation
        JOIN commercial.inventory_suppliers supplier
          ON supplier.tenant_id = invitation.tenant_id
         AND supplier.id = invitation.supplier_id
        """;

    private const string ImpactSelect = """
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
        """;
}
