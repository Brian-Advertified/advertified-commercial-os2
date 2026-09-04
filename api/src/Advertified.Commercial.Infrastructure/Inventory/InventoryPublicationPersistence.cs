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
                    description, extension_json, audience_profile_json, deliverable_json,
                    spatial_json, coverage_geometry, catchment_geometry, route_geometry,
                    direction_geometry, verification_code, source_import_id,
                    source_candidate_id, published_by, published_at_utc)
                SELECT value."versionId", {tenantId.Value}, value."productId",
                    value."versionNumber", value."name", value."channel",
                    value."productType", value."geography", value."address",
                    value."latitude", value."longitude", value."description",
                    value."extensionJson"::jsonb, value."audienceProfileJson"::jsonb,
                    value."deliverableJson"::jsonb, value."spatialJson"::jsonb,
                    CASE WHEN value."coverageGeoJson" IS NULL THEN NULL ELSE
                        ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(value."coverageGeoJson"), 4326)) END,
                    CASE WHEN value."catchmentGeoJson" IS NULL THEN NULL ELSE
                        ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(value."catchmentGeoJson"), 4326)) END,
                    CASE WHEN value."routeGeoJson" IS NULL THEN NULL ELSE
                        ST_Multi(ST_SetSRID(ST_GeomFromGeoJSON(value."routeGeoJson"), 4326)) END,
                    CASE WHEN value."directionGeoJson" IS NULL THEN NULL ELSE
                        ST_SetSRID(ST_GeomFromGeoJSON(value."directionGeoJson"), 4326) END,
                    {MasterDataCodes.VerificationLevels.HumanVerified}, {importId},
                    value."candidateId", {publishedBy}, {now}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "productId" uuid, "versionId" uuid, "versionNumber" integer,
                    "candidateId" uuid, "name" text, "channel" text,
                    "productType" text, "geography" text, "address" text,
                    "latitude" numeric, "longitude" numeric, "description" text,
                    "extensionJson" text, "audienceProfileJson" text,
                    "deliverableJson" text, "spatialJson" text,
                    "coverageGeoJson" text, "catchmentGeoJson" text,
                    "routeGeoJson" text, "directionGeoJson" text);

                INSERT INTO commercial.inventory_rates (
                    id, tenant_id, product_version_id, rate_type_code,
                    currency_code, amount_minor, effective_from, effective_to,
                    vat_treatment_code, commercial_terms_json, source_locator)
                SELECT value."rateId", {tenantId.Value}, value."versionId",
                    value."rateType", value."currency", value."rateAmountMinor",
                    value."rateValidFrom", value."rateValidTo", value."vatTreatment",
                    value."commercialTermsJson"::jsonb, value."sourceLocator"
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "rateId" uuid, "versionId" uuid, "rateType" text,
                    "currency" text, "rateAmountMinor" bigint,
                    "rateValidFrom" date, "rateValidTo" date, "vatTreatment" text,
                    "commercialTermsJson" text, "sourceLocator" text);

                INSERT INTO commercial.inventory_product_points_of_interest (
                    id, tenant_id, product_version_id, name, category, location,
                    source_import_id)
                SELECT gen_random_uuid(), {tenantId.Value}, value."versionId",
                    poi."name", poi."category",
                    CASE WHEN poi."latitude" IS NULL OR poi."longitude" IS NULL THEN NULL
                        ELSE ST_SetSRID(ST_MakePoint(
                            poi."longitude", poi."latitude"), 4326)::geography END,
                    {importId}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "versionId" uuid, "spatialJson" text)
                CROSS JOIN LATERAL jsonb_to_recordset(
                    COALESCE(value."spatialJson"::jsonb->'pointsOfInterest', '[]'::jsonb))
                    AS poi("name" text, "category" text,
                        "latitude" numeric, "longitude" numeric)
                WHERE value."spatialJson" IS NOT NULL;

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
                    "objectKey" text, "sourceHash" text, "mediaType" text)
                WHERE value."assetId" IS NOT NULL;

                INSERT INTO commercial.inventory_packages (
                    id, tenant_id, supplier_id, package_code, version_number, name,
                    rate_id, discount_rule, conditions_json, source_import_id)
                SELECT value."packageId", {tenantId.Value}, {supplierId},
                    value."packageCode",
                    COALESCE((SELECT MAX(existing.version_number) + 1
                        FROM commercial.inventory_packages existing
                        WHERE existing.tenant_id = {tenantId.Value}
                          AND existing.supplier_id = {supplierId}
                          AND existing.package_code = value."packageCode"), 1),
                    value."packageName", value."rateId", value."packageDiscountRule",
                    value."packageConditionsJson"::jsonb, {importId}
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "packageId" uuid, "packageCode" text, "packageName" text,
                    "rateId" uuid, "packageDiscountRule" text,
                    "packageConditionsJson" text)
                WHERE value."packageId" IS NOT NULL;

                INSERT INTO commercial.inventory_package_components (
                    id, tenant_id, package_id, product_id)
                SELECT gen_random_uuid(), {tenantId.Value}, value."packageId", product.id
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "packageId" uuid, "packageComponentCodesJson" text)
                CROSS JOIN LATERAL jsonb_array_elements_text(
                    value."packageComponentCodesJson"::jsonb) component(code)
                JOIN commercial.inventory_products product
                  ON product.tenant_id = {tenantId.Value}
                 AND product.supplier_id = {supplierId}
                 AND product.supplier_product_code = component.code
                WHERE value."packageId" IS NOT NULL;
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
            await DetectExactDuplicateCandidatesAsync(
                dbContext, tenantId, batch.Select(item => item.ProductId).ToArray(),
                now, cancellationToken);
        }
    }

    private static Task<int> DetectExactDuplicateCandidatesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid[] productIds,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_duplicate_candidates (
                id, tenant_id, left_product_id, right_product_id,
                left_product_version_id, right_product_version_id,
                method_code, similarity, evidence_json, status_code,
                detected_at_utc, version)
            SELECT gen_random_uuid(), {tenantId.Value},
                LEAST(source.id, peer.id), GREATEST(source.id, peer.id),
                CASE WHEN source.id < peer.id THEN source_version.id ELSE peer_version.id END,
                CASE WHEN source.id < peer.id THEN peer_version.id ELSE source_version.id END,
                {MasterDataCodes.InventoryDuplicateMethods.ExactNameLocation}, 1,
                jsonb_build_object('name', source_version.name,
                    'geography', source_version.geography,
                    'productType', source_version.product_type_code),
                {MasterDataCodes.InventoryDuplicateStatuses.Open}, {now}, 1
            FROM commercial.inventory_products source
            JOIN commercial.inventory_product_versions source_version
              ON source_version.tenant_id = source.tenant_id
             AND source_version.id = source.current_version_id
            JOIN commercial.inventory_product_versions peer_version
              ON peer_version.tenant_id = source_version.tenant_id
             AND lower(btrim(peer_version.name)) = lower(btrim(source_version.name))
             AND lower(btrim(peer_version.geography)) = lower(btrim(source_version.geography))
             AND peer_version.product_type_code = source_version.product_type_code
            JOIN commercial.inventory_products peer
              ON peer.tenant_id = peer_version.tenant_id
             AND peer.current_version_id = peer_version.id
             AND peer.id <> source.id
             AND peer.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            WHERE source.tenant_id = {tenantId.Value}
              AND source.id = ANY({productIds})
              AND source.status_code = {MasterDataCodes.LifecycleStatuses.Active}
            ON CONFLICT (
                tenant_id, left_product_version_id, right_product_version_id, method_code)
                DO NOTHING
            """, cancellationToken);
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
    string? Description,
    string ExtensionJson,
    string? AudienceProfileJson,
    string? DeliverableJson,
    string? SpatialJson,
    string? CoverageGeoJson,
    string? CatchmentGeoJson,
    string? RouteGeoJson,
    string? DirectionGeoJson,
    Guid RateId,
    string? RateType,
    string? Currency,
    long? RateAmountMinor,
    DateOnly? RateValidFrom,
    DateOnly? RateValidTo,
    string? VatTreatment,
    string? CommercialTermsJson,
    Guid AvailabilityId,
    string Availability,
    Guid? AssetId,
    string? AssetType,
    string? ObjectKey,
    string? SourceHash,
    string? MediaType,
    string SourceLocator,
    Guid? PackageId,
    string? PackageCode,
    string? PackageName,
    string? PackageComponentCodesJson,
    string? PackageDiscountRule,
    string? PackageConditionsJson);
