using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryReader
{
    public async Task<IReadOnlyList<InventoryPlaceView>> SearchPlacesAsync(
        ActorId actorId, TenantId tenantId, string search, CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId,
            MasterDataReferences.Permissions.InventoryView, cancellationToken);
        var term = search.Trim();
        if (term.Length is < 2 or > 200)
            throw new ArgumentException("Supply a place name between 2 and 200 characters.", nameof(search));
        await using var transaction = await store.BeginSessionAsync(actorId, tenantId, cancellationToken);
        var supplierScope = await supplierAccess.ResolveSupplierScopeAsync(actorId, tenantId, cancellationToken);
        var rows = await store.DbContext.Database.SqlQuery<InventoryPlaceView>($"""
            SELECT poi.id AS "Id", poi.name AS "Name", poi.category AS "Category",
                version.geography AS "Context",
                ST_Y(poi.location::geometry)::numeric AS "Latitude",
                ST_X(poi.location::geometry)::numeric AS "Longitude",
                version.id AS "ProductVersionId", poi.source_import_id AS "SourceImportId",
                candidate.source_locator AS "SourceLocator"
            FROM commercial.inventory_product_points_of_interest poi
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id=poi.tenant_id AND version.id=poi.product_version_id
            JOIN commercial.inventory_products product
              ON product.tenant_id=version.tenant_id AND product.id=version.product_id
             AND product.current_version_id=version.id
            JOIN commercial.inventory_candidates candidate
              ON candidate.tenant_id=version.tenant_id AND candidate.id=version.source_candidate_id
            WHERE poi.tenant_id={tenantId.Value} AND poi.location IS NOT NULL
              AND product.status_code={MasterDataCodes.LifecycleStatuses.Active}
              AND ({supplierScope}::uuid[] IS NULL OR product.supplier_id=ANY({supplierScope}))
              AND strpos(lower(poi.name), lower({term})) > 0
            ORDER BY lower(poi.name), version.geography, poi.id LIMIT 20
            """).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows;
    }
}
