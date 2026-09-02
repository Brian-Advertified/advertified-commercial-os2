using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryReader
{
    private Task<List<InventoryAssetRow>> ListAssetsAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryAssetRow>($"""
            SELECT asset.id AS "Id", asset.asset_type_code AS "AssetType",
                asset.media_type AS "MediaType", asset.content_hash AS "ContentHash",
                'inventory-import:' || asset.source_import_id::text AS "SourceReference",
                COALESCE(rights.rights_status_code,
                    {MasterDataCodes.AssetRightsStatuses.Unknown}) AS "RightsStatus",
                rights.rights_basis AS "RightsBasis",
                rights.licensed_until AS "LicensedUntil",
                COALESCE(rights.asset_version, 1) AS "RightsVersion",
                COALESCE(rights.scope_codes, '[]')::text AS "RightsScopesJson",
                COALESCE(rights.territory_code, 'ZA') AS "TerritoryCode",
                rights.effective_on AS "EffectiveOn",
                COALESCE(rights.until_revoked, false) AS "UntilRevoked"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_assets asset
             ON asset.tenant_id = product.tenant_id
             AND asset.product_version_id = product.current_version_id
            LEFT JOIN LATERAL (
                SELECT review.* FROM commercial.inventory_asset_rights_reviews review
                WHERE review.tenant_id = asset.tenant_id AND review.asset_id = asset.id
                ORDER BY review.asset_version DESC LIMIT 1) rights ON TRUE
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
            ORDER BY asset.id
            """).ToListAsync(cancellationToken);
}
