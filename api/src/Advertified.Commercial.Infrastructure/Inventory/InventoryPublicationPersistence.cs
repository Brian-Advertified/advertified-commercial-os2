using System.Runtime.CompilerServices;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryPublicationPersistence
{
    private const int BatchSize = 250;

    internal static Task<int> LockSupplierAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenantId.Value + ":" + supplierId}, 0))",
            cancellationToken);

    internal static Task<List<ExistingInventoryProductRow>> LoadProductsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        string[] productCodes,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<ExistingInventoryProductRow>($"""
            SELECT id AS "Id", supplier_product_code AS "ProductCode",
                version AS "Version"
            FROM commercial.inventory_products
            WHERE tenant_id = {tenantId.Value} AND supplier_id = {supplierId}
              AND supplier_product_code = ANY({productCodes})
            FOR UPDATE
            """).ToListAsync(cancellationToken);

    internal static Task<List<InventoryProductVersionNumberRow>> LoadNextVersionsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid[] productIds,
        CancellationToken cancellationToken)
    {
        if (productIds.Length == 0)
        {
            return Task.FromResult(new List<InventoryProductVersionNumberRow>());
        }
        return dbContext.Database.SqlQuery<InventoryProductVersionNumberRow>($"""
            SELECT product_id AS "ProductId",
                (MAX(version_number) + 1)::integer AS "NextVersionNumber"
            FROM commercial.inventory_product_versions
            WHERE tenant_id = {tenantId.Value} AND product_id = ANY({productIds})
            GROUP BY product_id
            """).ToListAsync(cancellationToken);
    }

    internal static async Task PersistAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        Guid publishedBy,
        DateTimeOffset now,
        IReadOnlyList<PreparedInventoryPublication> publications,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < publications.Count; offset += BatchSize)
        {
            var batch = publications.Skip(offset).Take(BatchSize).ToArray();
            var payload = JsonSerializer.Serialize(batch, InventoryRowMapper.StoredJson);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_products (
                    id, tenant_id, supplier_id, supplier_product_code, status_code,
                    version, created_at_utc, updated_at_utc)
                SELECT value."productId", {tenantId.Value}, {supplierId},
                    value."productCode", {MasterDataCodes.LifecycleStatuses.Active},
                    1, {now}, {now}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "productId" uuid, "productCode" text, "isNew" boolean)
                WHERE value."isNew";

                INSERT INTO commercial.inventory_product_versions (
                    id, tenant_id, product_id, version_number, name, channel_code,
                    product_type_code, geography, address, latitude, longitude,
                    extension_json, verification_code, source_import_id,
                    source_candidate_id, published_by, published_at_utc)
                SELECT value."versionId", {tenantId.Value}, value."productId",
                    value."versionNumber", value."name", value."channel",
                    value."productType", value."geography", value."address",
                    value."latitude", value."longitude", value."extensionJson"::jsonb,
                    {MasterDataCodes.VerificationLevels.HumanVerified}, {importId},
                    value."candidateId", {publishedBy}, {now}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "productId" uuid, "versionId" uuid, "versionNumber" integer,
                    "candidateId" uuid, "name" text, "channel" text,
                    "productType" text, "geography" text, "address" text,
                    "latitude" numeric, "longitude" numeric, "extensionJson" text);

                INSERT INTO commercial.inventory_rates (
                    id, tenant_id, product_version_id, rate_type_code,
                    currency_code, amount_minor, source_locator)
                SELECT value."rateId", {tenantId.Value}, value."versionId",
                    value."rateType", value."currency", value."rateAmountMinor",
                    value."sourceLocator"
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "rateId" uuid, "versionId" uuid, "rateType" text,
                    "currency" text, "rateAmountMinor" bigint,
                    "sourceLocator" text);

                INSERT INTO commercial.inventory_availability (
                    id, tenant_id, product_version_id, availability_code,
                    observed_at_utc, source_locator)
                SELECT value."availabilityId", {tenantId.Value}, value."versionId",
                    value."availability", {now}, value."sourceLocator"
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "availabilityId" uuid, "versionId" uuid,
                    "availability" text, "sourceLocator" text);

                INSERT INTO commercial.inventory_assets (
                    id, tenant_id, product_version_id, asset_type_code, object_key,
                    content_hash, media_type, source_import_id)
                SELECT value."assetId", {tenantId.Value}, value."versionId",
                    value."assetType", value."objectKey", value."sourceHash",
                    value."mediaType", {importId}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "assetId" uuid, "versionId" uuid, "assetType" text,
                    "objectKey" text, "sourceHash" text, "mediaType" text);
                """, cancellationToken);

            var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_products product
                SET current_version_id = value."versionId",
                    status_code = {MasterDataCodes.LifecycleStatuses.Active},
                    version = product.version + CASE WHEN value."isNew" THEN 0 ELSE 1 END,
                    updated_at_utc = {now}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "productId" uuid, "versionId" uuid, "isNew" boolean)
                WHERE product.tenant_id = {tenantId.Value}
                  AND product.id = value."productId"
                """, cancellationToken);
            if (changed != batch.Length)
            {
                throw new VersionConflictException();
            }
        }
    }
}

internal sealed record ExistingInventoryProductRow
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public long Version { get; set; }
}

internal sealed record InventoryProductVersionNumberRow
{
    public Guid ProductId { get; set; }
    public int NextVersionNumber { get; set; }
}

internal sealed record PreparedInventoryPublication(
    Guid ProductId,
    string ProductCode,
    bool IsNew,
    Guid VersionId,
    int VersionNumber,
    Guid CandidateId,
    string Name,
    string Channel,
    string ProductType,
    string Geography,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    string ExtensionJson,
    Guid RateId,
    string RateType,
    string Currency,
    long RateAmountMinor,
    Guid AvailabilityId,
    string Availability,
    Guid AssetId,
    string AssetType,
    string ObjectKey,
    string SourceHash,
    string MediaType,
    string SourceLocator);
