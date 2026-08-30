using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryReader(
    InventoryRecordStore store,
    ITenantAuthorizer authorizer,
    TimeProvider timeProvider,
    IOptions<InventoryProtectionOptions> protectionOptions) : IInventoryReader
{
    private readonly int maximumSourceBytes = protectionOptions.Value.MaximumSourceBytes;

    public async Task<InventoryImportView> GetImportAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid importId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryImport, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindImportAsync(tenantId, importId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        var view = await store.BuildImportViewAsync(
            row, pageSize, cursor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<InventoryProductPage> SearchAsync(
        ActorId actorId,
        TenantId tenantId,
        InventorySearchQuery query,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryView, cancellationToken);
        var validated = InventoryQueryPolicy.Validate(query);
        var pageSize = validated.PageSize;
        var cursor = InventoryCursor.Decode(validated.Cursor);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await SearchRowsAsync(
            tenantId, validated, cursor, pageSize + 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var page = rows.Take(pageSize).ToArray();
        var next = rows.Count > pageSize
            ? InventoryCursor.Encode(page[^1].Name.ToLowerInvariant(), page[^1].Id) : null;
        return new InventoryProductPage(
            page.Select(InventoryRowMapper.ToView).ToArray(), next, maximumSourceBytes);
    }

    public async Task<InventoryProductView> GetProductAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryView, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var summary = await FindSummaryAsync(tenantId, productId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        var now = timeProvider.GetUtcNow();
        var detail = await FindDetailAsync(tenantId, productId, now, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory product access denied.");
        var assets = await ListAssetsAsync(tenantId, productId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProductView(summary, detail, assets);
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Inventory access denied.");
        }
    }

    private Task<List<InventoryProductSummaryRow>> SearchRowsAsync(
        TenantId tenantId,
        InventorySearchQuery query,
        InventoryCursorValue? cursor,
        int take,
        CancellationToken cancellationToken)
    {
        var search = query.Search;
        var channel = query.Channel;
        var supplier = query.Supplier;
        var geography = query.Geography;
        var format = SummarySelect + Environment.NewLine + (cursor is null
            ? """
                WHERE product.tenant_id = {0}
                  AND product.status_code = {1}
                  AND ({2}::text IS NULL OR version.name ILIKE '%' || {2} || '%'
                       OR product.supplier_product_code ILIKE '%' || {2} || '%')
                  AND ({3}::text IS NULL OR version.channel_code = {3})
                  AND ({4}::text IS NULL OR supplier.name ILIKE '%' || {4} || '%')
                  AND ({5}::text IS NULL OR version.geography ILIKE '%' || {5} || '%')
                ORDER BY lower(version.name), version.product_id LIMIT {6}
                """
            : """
                WHERE product.tenant_id = {0}
                  AND product.status_code = {1}
                  AND ({2}::text IS NULL OR version.name ILIKE '%' || {2} || '%'
                       OR product.supplier_product_code ILIKE '%' || {2} || '%')
                  AND ({3}::text IS NULL OR version.channel_code = {3})
                  AND ({4}::text IS NULL OR supplier.name ILIKE '%' || {4} || '%')
                  AND ({5}::text IS NULL OR version.geography ILIKE '%' || {5} || '%')
                  AND (lower(version.name), version.product_id) > ({6}, {7})
                ORDER BY lower(version.name), version.product_id LIMIT {8}
                """);
        var arguments = cursor is null
            ? new object?[] { tenantId.Value, MasterDataCodes.LifecycleStatuses.Active,
                search, channel, supplier, geography, take }
            : [tenantId.Value, MasterDataCodes.LifecycleStatuses.Active,
                search, channel, supplier, geography, cursor.Name, cursor.Id, take];
        var statement = FormattableStringFactory.Create(format, arguments);
        return store.DbContext.Database.SqlQuery<InventoryProductSummaryRow>(statement)
            .ToListAsync(cancellationToken);
    }

    private Task<InventoryProductSummaryRow?> FindSummaryAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryProductSummaryRow>(
            FormattableStringFactory.Create(
                SummarySelect + Environment.NewLine +
                    "WHERE product.tenant_id = {0} AND product.id = {1}",
                tenantId.Value, productId)).SingleOrDefaultAsync(cancellationToken);

    private Task<InventoryProductDetailRow?> FindDetailAsync(
        TenantId tenantId,
        Guid productId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryProductDetailRow>($"""
            SELECT version.address AS "Address", version.latitude AS "Latitude",
                version.longitude AS "Longitude", version.extension_json::text AS "ExtensionJson",
                rate.rate_type_code AS "RateType", rate.currency_code AS "Currency",
                rate.amount_minor AS "AmountMinor", rate.source_locator AS "RateLocator",
                availability.availability_code AS "Availability",
                availability.observed_at_utc AS "ObservedAtUtc",
                availability.valid_until_utc AS "ValidUntilUtc",
                availability.source_locator AS "AvailabilityLocator",
                version.source_import_id AS "SourceImportId",
                version.source_candidate_id AS "SourceCandidateId",
                version.version_number AS "VersionNumber",
                version.published_at_utc AS "PublishedAtUtc"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            JOIN LATERAL (
                SELECT item.* FROM commercial.inventory_rates item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                  AND (item.effective_from IS NULL OR item.effective_from <=
                    {DateOnly.FromDateTime(now.UtcDateTime)})
                  AND (item.effective_to IS NULL OR item.effective_to >=
                    {DateOnly.FromDateTime(now.UtcDateTime)})
                ORDER BY item.effective_from DESC NULLS LAST, item.id DESC
                LIMIT 1) rate ON TRUE
            JOIN LATERAL (
                SELECT item.* FROM commercial.inventory_availability item
                WHERE item.tenant_id = version.tenant_id
                  AND item.product_version_id = version.id
                  AND (item.observed_at_utc IS NULL OR item.observed_at_utc <= {now})
                ORDER BY item.observed_at_utc DESC NULLS LAST, item.id DESC
                LIMIT 1) availability ON TRUE
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<List<InventoryAssetRow>> ListAssetsAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryAssetRow>($"""
            SELECT asset.asset_type_code AS "AssetType", asset.media_type AS "MediaType",
                asset.content_hash AS "ContentHash",
                'inventory-import:' || asset.source_import_id::text AS "SourceReference"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_assets asset
              ON asset.tenant_id = product.tenant_id
             AND asset.product_version_id = product.current_version_id
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
            ORDER BY asset.id
            """).ToListAsync(cancellationToken);

    private static InventoryProductView ToProductView(
        InventoryProductSummaryRow summary,
        InventoryProductDetailRow detail,
        IReadOnlyList<InventoryAssetRow> assets) => new(
        summary.ToView(), detail.Address, detail.Latitude, detail.Longitude,
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            detail.ExtensionJson, InventoryRowMapper.StoredJson) ?? [],
        new InventoryRateView(
            detail.RateType, detail.Currency, detail.AmountMinor, detail.RateLocator),
        new InventoryAvailabilityView(
            detail.Availability, detail.ObservedAtUtc, detail.ValidUntilUtc,
            detail.AvailabilityLocator),
        assets.Select(item => new InventoryAssetView(
            item.AssetType, item.MediaType, item.ContentHash, item.SourceReference)).ToArray(),
        detail.SourceImportId, detail.SourceCandidateId,
        detail.VersionNumber, detail.PublishedAtUtc);

    private const string SummarySelect = """
        SELECT product.id AS "Id", product.supplier_id AS "SupplierId",
            supplier.name AS "SupplierName",
            product.supplier_product_code AS "ProductCode",
            version.name AS "Name", version.channel_code AS "Channel",
            version.product_type_code AS "ProductType", version.geography AS "Geography",
            version.verification_code AS "Verification", product.version AS "Version",
            product.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.inventory_products product
        JOIN commercial.inventory_suppliers supplier
          ON supplier.tenant_id = product.tenant_id AND supplier.id = product.supplier_id
        JOIN commercial.inventory_product_versions version
          ON version.tenant_id = product.tenant_id
         AND version.id = product.current_version_id
         AND version.product_id = product.id
        """;
}
