using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryReleaseCutover(
    Guid ReleaseId,
    Guid? PreviousReleaseId,
    int VersionNumber,
    long? PreviousAggregateVersion);

internal static partial class InventorySupplierReleasePublication
{
    internal static async Task<InventoryReleaseCutover> BeginAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        string replacementMode,
        Guid publishedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.Database
            .SqlQuery<CurrentInventoryReleaseRow>($"""
                SELECT supplier.current_inventory_release_id AS "ReleaseId",
                    COALESCE(release.version_number, 0)::integer AS "VersionNumber",
                    COALESCE(release.version, 0)::bigint AS "AggregateVersion"
                FROM commercial.inventory_suppliers supplier
                LEFT JOIN commercial.inventory_supplier_releases release
                  ON release.tenant_id = supplier.tenant_id
                 AND release.id = supplier.current_inventory_release_id
                WHERE supplier.tenant_id = {tenantId.Value}
                  AND supplier.id = {supplierId}
                FOR UPDATE OF supplier
                """)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Supplier access denied.");

        if (current.ReleaseId.HasValue)
        {
            await SupersedeCurrentReleaseAsync(
                dbContext, tenantId, current.ReleaseId.Value, now,
                cancellationToken);
        }

        var release = new InventoryReleaseCutover(
            Guid.NewGuid(), current.ReleaseId, current.VersionNumber + 1,
            current.ReleaseId.HasValue ? current.AggregateVersion + 1 : null);
        await InsertReleaseAsync(
            dbContext, tenantId, supplierId, importId, replacementMode,
            publishedBy, now, release, cancellationToken);
        return release;
    }

    internal static async Task<int> CompleteAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        Guid publishedBy,
        DateTimeOffset now,
        InventoryReleaseCutover release,
        CancellationToken cancellationToken)
    {
        if (release.PreviousReleaseId.HasValue)
        {
            await SupersedePreviousInventoryAsync(
                dbContext, tenantId, supplierId,
                release.PreviousReleaseId.Value, release.ReleaseId,
                now, cancellationToken);
        }

        await SwitchSupplierReleaseAsync(
            dbContext, tenantId, supplierId, release.ReleaseId,
            now, cancellationToken);
        await SupersedeEarlierPendingWorkAsync(
            dbContext, tenantId, supplierId, importId, publishedBy,
            now, cancellationToken);

        return release.PreviousReleaseId.HasValue
            ? await RegisterProposalImpactsAsync(
                dbContext, tenantId, supplierId,
                release.PreviousReleaseId.Value, release.ReleaseId,
                publishedBy, now, cancellationToken)
            : 0;
    }

    private static async Task SupersedeCurrentReleaseAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid releaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_supplier_releases
            SET status_code = {MasterDataCodes.InventoryReleaseStatuses.Superseded},
                superseded_at_utc = {now}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {releaseId}
              AND status_code = {MasterDataCodes.InventoryReleaseStatuses.Current}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private static Task<int> InsertReleaseAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        string replacementMode,
        Guid publishedBy,
        DateTimeOffset now,
        InventoryReleaseCutover release,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_supplier_releases (
                id, tenant_id, supplier_id, source_import_id, version_number,
                replacement_mode_code, status_code, supersedes_release_id,
                effective_at_utc, created_by, version, created_at_utc, updated_at_utc)
            VALUES ({release.ReleaseId}, {tenantId.Value}, {supplierId}, {importId},
                {release.VersionNumber}, {replacementMode},
                {MasterDataCodes.InventoryReleaseStatuses.Current},
                {release.PreviousReleaseId}, {now}, {publishedBy}, 1, {now}, {now})
            """, cancellationToken);

    private static async Task SupersedePreviousInventoryAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid previousReleaseId,
        Guid replacementReleaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await LinkReplacementVersionsAsync(
            dbContext, tenantId, previousReleaseId, replacementReleaseId,
            now, cancellationToken);
        await MarkRemainingVersionsAndPackagesAsync(
            dbContext, tenantId, previousReleaseId, now, cancellationToken);
        await ArchiveSupersededListingsAsync(
            dbContext, tenantId, previousReleaseId, replacementReleaseId,
            now, cancellationToken);
        await ExpireRemovedProductsAsync(
            dbContext, tenantId, supplierId, previousReleaseId,
            replacementReleaseId, now, cancellationToken);
    }

    private static Task<int> LinkReplacementVersionsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid previousReleaseId,
        Guid replacementReleaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_product_versions previous
            SET superseded_by_version_id = replacement.id,
                superseded_at_utc = {now}
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions replacement
              ON replacement.tenant_id = product.tenant_id
             AND replacement.id = product.current_version_id
            WHERE previous.tenant_id = {tenantId.Value}
              AND previous.inventory_release_id = {previousReleaseId}
              AND previous.product_id = product.id
              AND product.current_release_id = {replacementReleaseId}
              AND replacement.inventory_release_id = {replacementReleaseId}
              AND previous.superseded_at_utc IS NULL
            """, cancellationToken);

    private static Task<int> MarkRemainingVersionsAndPackagesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid previousReleaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_product_versions
            SET superseded_at_utc = {now}
            WHERE tenant_id = {tenantId.Value}
              AND inventory_release_id = {previousReleaseId}
              AND superseded_at_utc IS NULL;

            UPDATE commercial.inventory_packages
            SET superseded_at_utc = {now}
            WHERE tenant_id = {tenantId.Value}
              AND inventory_release_id = {previousReleaseId}
              AND superseded_at_utc IS NULL;
            """, cancellationToken);

    private static Task<int> ArchiveSupersededListingsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid previousReleaseId,
        Guid replacementReleaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.marketplace_listings listing
            SET status_code = {MasterDataCodes.MarketplaceListingStatuses.Archived},
                archived_reason = {"Supplier inventory was replaced."},
                superseded_by_release_id = {replacementReleaseId},
                superseded_at_utc = {now}, soft_deleted_at_utc = {now},
                version = version + 1, updated_at_utc = {now}
            FROM commercial.marketplace_listing_versions snapshot
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = snapshot.supplier_tenant_id
             AND version.id = snapshot.product_version_id
            WHERE listing.supplier_tenant_id = {tenantId.Value}
              AND listing.current_version_id = snapshot.id
              AND version.inventory_release_id = {previousReleaseId}
              AND listing.soft_deleted_at_utc IS NULL
            """, cancellationToken);

    private static Task<int> ExpireRemovedProductsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid previousReleaseId,
        Guid replacementReleaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET status_code = {MasterDataCodes.LifecycleStatuses.Expired},
                superseded_by_release_id = {replacementReleaseId},
                expired_at_utc = {now}, version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND supplier_id = {supplierId}
              AND current_release_id = {previousReleaseId}
              AND status_code <> {MasterDataCodes.LifecycleStatuses.Expired}
            """, cancellationToken);

    private static async Task SwitchSupplierReleaseAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid releaseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_suppliers
            SET current_inventory_release_id = {releaseId},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {supplierId}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private static Task<int> RegisterProposalImpactsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid previousReleaseId,
        Guid replacementReleaseId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<int>($"""
            SELECT commercial.register_supplier_inventory_replacement_impacts(
                {tenantId.Value}, {supplierId}, {previousReleaseId},
                {replacementReleaseId}, {actorId}, {now}) AS "Value"
            """).SingleAsync(cancellationToken);

    private sealed record CurrentInventoryReleaseRow(
        Guid? ReleaseId,
        int VersionNumber,
        long AggregateVersion);
}
