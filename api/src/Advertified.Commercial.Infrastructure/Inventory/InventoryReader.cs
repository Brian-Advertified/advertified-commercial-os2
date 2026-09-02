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

public sealed partial class InventoryReader(
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
        var contacts = await ListSupplierContactsAsync(tenantId, summary.SupplierId, cancellationToken);
        var packages = await ListPackagesAsync(tenantId, productId, cancellationToken);
        var exceptions = await ListAvailabilityExceptionsAsync(
            tenantId, productId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProductView(summary, detail, assets, contacts, packages, exceptions, now);
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
                  AND NOT EXISTS (
                      SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                      WHERE identity_link.tenant_id = product.tenant_id
                        AND identity_link.duplicate_product_id = product.id)
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
                  AND NOT EXISTS (
                      SELECT 1 FROM commercial.inventory_product_identity_links identity_link
                      WHERE identity_link.tenant_id = product.tenant_id
                        AND identity_link.duplicate_product_id = product.id)
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
            SELECT version.id AS "ProductVersionId", version.address AS "Address",
                version.latitude AS "Latitude",
                version.longitude AS "Longitude", version.extension_json::text AS "ExtensionJson",
                version.description AS "Description",
                version.deliverable_json::text AS "DeliverableJson",
                version.spatial_json::text AS "SpatialJson",
                version.audience_profile_json::text AS "AudienceProfileJson",
                candidate.source_locator AS "AudienceSourceLocator",
                rate.rate_type_code AS "RateType", rate.currency_code AS "Currency",
                rate.amount_minor AS "AmountMinor", rate.source_locator AS "RateLocator",
                rate.effective_from AS "EffectiveFrom", rate.effective_to AS "EffectiveTo",
                rate.vat_treatment_code AS "VatTreatment",
                rate.commercial_terms_json::text AS "CommercialTermsJson",
                availability.availability_code AS "Availability",
                availability.observed_at_utc AS "ObservedAtUtc",
                availability.valid_until_utc AS "ValidUntilUtc",
                availability.source_locator AS "AvailabilityLocator",
                version.source_import_id AS "SourceImportId",
                version.source_candidate_id AS "SourceCandidateId",
                version.version_number AS "VersionNumber",
                version.published_at_utc AS "PublishedAtUtc",
                supplier_version.version_number AS "SupplierVersionNumber",
                supplier_version.vat_status_code AS "SupplierVatStatus",
                supplier_version.vat_number AS "SupplierVatNumber",
                supplier_version.commission_terms AS "SupplierCommissionTerms",
                supplier_version.payment_terms AS "SupplierPaymentTerms",
                supplier_version.cancellation_terms AS "SupplierCancellationTerms",
                supplier_version.booking_deadline_terms AS "SupplierBookingDeadlineTerms",
                supplier_version.source_import_id AS "SupplierSourceImportId",
                supplier_version.published_at_utc AS "SupplierPublishedAtUtc"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            JOIN commercial.inventory_candidates candidate
              ON candidate.tenant_id = version.tenant_id
             AND candidate.id = version.source_candidate_id
            LEFT JOIN commercial.inventory_suppliers supplier
              ON supplier.tenant_id = product.tenant_id AND supplier.id = product.supplier_id
            LEFT JOIN commercial.inventory_supplier_versions supplier_version
              ON supplier_version.tenant_id = supplier.tenant_id
             AND supplier_version.id = supplier.current_commercial_version_id
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

    private Task<List<InventorySupplierContactRow>> ListSupplierContactsAsync(
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventorySupplierContactRow>($"""
            SELECT id AS "Id", name AS "Name", role AS "Role", region AS "Region",
                email AS "Email", phone AS "Phone", website AS "Website",
                social_handle AS "SocialHandle", observed_at_utc AS "ObservedAtUtc"
            FROM commercial.inventory_supplier_contacts
            WHERE tenant_id = {tenantId.Value} AND supplier_id = {supplierId}
            ORDER BY observed_at_utc DESC, id LIMIT 100
            """).ToListAsync(cancellationToken);

    private Task<List<InventoryAvailabilityExceptionRow>> ListAvailabilityExceptionsAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryAvailabilityExceptionRow>($"""
            SELECT exception.id AS "Id", product.id AS "ProductId",
                exception.product_version_id AS "ProductVersionId",
                exception.exception_type_code AS "ExceptionType",
                exception.starts_on AS "StartsOn", exception.ends_on AS "EndsOn",
                exception.source_locator AS "SourceLocator",
                exception.evidence_hash AS "EvidenceHash",
                exception.recorded_by AS "RecordedBy",
                exception.recorded_at_utc AS "RecordedAtUtc"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_availability_exceptions exception
              ON exception.tenant_id = product.tenant_id
             AND exception.product_id = product.id
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
            ORDER BY exception.starts_on, exception.ends_on, exception.id
            """).ToListAsync(cancellationToken);

    private Task<List<InventoryPackageRow>> ListPackagesAsync(
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<InventoryPackageRow>($"""
            SELECT package.id AS "Id", package.package_code AS "PackageCode",
                package.version_number AS "VersionNumber", package.name AS "Name",
                package.discount_rule AS "DiscountRule",
                package.conditions_json::text AS "ConditionsJson",
                COALESCE(jsonb_agg(component_product.supplier_product_code)
                    FILTER (WHERE component_product.id IS NOT NULL), '[]')::text
                    AS "ComponentProductCodesJson"
            FROM commercial.inventory_packages package
            JOIN commercial.inventory_rates rate
              ON rate.tenant_id = package.tenant_id AND rate.id = package.rate_id
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = rate.tenant_id AND version.id = rate.product_version_id
            LEFT JOIN commercial.inventory_package_components component
              ON component.tenant_id = package.tenant_id AND component.package_id = package.id
            LEFT JOIN commercial.inventory_products component_product
              ON component_product.tenant_id = component.tenant_id
             AND component_product.id = component.product_id
            WHERE package.tenant_id = {tenantId.Value}
              AND (version.product_id = {productId} OR component.product_id = {productId})
            GROUP BY package.id, package.package_code, package.version_number,
                package.name, package.discount_rule, package.conditions_json
            ORDER BY package.package_code, package.version_number DESC
            """).ToListAsync(cancellationToken);

    private static InventoryProductView ToProductView(
        InventoryProductSummaryRow summary,
        InventoryProductDetailRow detail,
        IReadOnlyList<InventoryAssetRow> assets,
        IReadOnlyList<InventorySupplierContactRow> contacts,
        IReadOnlyList<InventoryPackageRow> packages,
        IReadOnlyList<InventoryAvailabilityExceptionRow> exceptions,
        DateTimeOffset now) => new(
        summary.ToView(), detail.ProductVersionId, detail.Address,
        detail.Latitude, detail.Longitude,
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            detail.ExtensionJson, InventoryRowMapper.StoredJson) ?? [],
        new InventoryRateView(
            detail.RateType, detail.Currency, detail.AmountMinor, detail.RateLocator,
            detail.EffectiveFrom, detail.EffectiveTo, detail.VatTreatment,
            Read<InventoryCommercialTermsValues>(detail.CommercialTermsJson)),
        new InventoryAvailabilityView(
            detail.Availability, detail.ObservedAtUtc, detail.ValidUntilUtc,
            detail.AvailabilityLocator),
        ToAudienceView(detail),
        assets.Select(item => new InventoryAssetView(
            item.AssetType, item.MediaType, item.ContentHash, item.SourceReference,
            item.Id, item.RightsStatus, item.RightsBasis, item.LicensedUntil,
            item.RightsStatus == MasterDataCodes.AssetRightsStatuses.Approved &&
            Read<string[]>(item.RightsScopesJson).Contains(
                MasterDataCodes.AssetRightsScopes.NamedClientProposal,
                StringComparer.Ordinal) &&
            item.EffectiveOn.HasValue && item.EffectiveOn <=
                DateOnly.FromDateTime(now.UtcDateTime) &&
            (item.UntilRevoked || item.LicensedUntil.HasValue &&
                item.LicensedUntil.Value >= DateOnly.FromDateTime(now.UtcDateTime)),
            item.RightsVersion,
            Read<string[]>(item.RightsScopesJson), item.TerritoryCode,
            item.EffectiveOn, item.UntilRevoked)).ToArray(),
        detail.SourceImportId, detail.SourceCandidateId,
        detail.VersionNumber, detail.PublishedAtUtc, detail.Description,
        ToSupplierCommercialView(detail), contacts.Select(item =>
            new InventorySupplierContactView(item.Id, item.Name, item.Role, item.Region,
                item.Email, item.Phone, item.Website, item.SocialHandle,
                item.ObservedAtUtc)).ToArray(),
        Read<InventoryDeliverableValues>(detail.DeliverableJson),
        Read<InventorySpatialValues>(detail.SpatialJson),
        packages.Select(ToPackageView).ToArray(),
        exceptions.Select(item => new InventoryAvailabilityExceptionView(
            item.Id, item.ProductId, item.ProductVersionId, item.ExceptionType,
            item.StartsOn, item.EndsOn, item.SourceLocator, item.EvidenceHash,
            item.RecordedBy, item.RecordedAtUtc, 1)).ToArray());

    private static InventorySupplierCommercialView? ToSupplierCommercialView(
        InventoryProductDetailRow detail) => !detail.SupplierVersionNumber.HasValue ||
        !detail.SupplierSourceImportId.HasValue || !detail.SupplierPublishedAtUtc.HasValue
        ? null
        : new(detail.SupplierVersionNumber.Value, detail.SupplierVatStatus,
            detail.SupplierVatNumber, detail.SupplierCommissionTerms,
            detail.SupplierPaymentTerms, detail.SupplierCancellationTerms,
            detail.SupplierBookingDeadlineTerms, detail.SupplierSourceImportId.Value,
            detail.SupplierPublishedAtUtc.Value);

    private static InventoryPackageView ToPackageView(InventoryPackageRow row) => new(
        row.Id, row.PackageCode, row.VersionNumber, row.Name, row.DiscountRule,
        JsonSerializer.Deserialize<string[]>(row.ConditionsJson,
            InventoryRowMapper.StoredJson) ?? [],
        JsonSerializer.Deserialize<string[]>(row.ComponentProductCodesJson,
            InventoryRowMapper.StoredJson) ?? []);

    private static T? Read<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(
            json, InventoryRowMapper.StoredJson);

    private static InventoryAudienceProfileView? ToAudienceView(
        InventoryProductDetailRow detail)
    {
        if (detail.AudienceProfileJson is null)
        {
            return null;
        }
        var profile = JsonSerializer.Deserialize<InventoryAudienceProfileValues>(
            detail.AudienceProfileJson, InventoryRowMapper.StoredJson)
            ?? throw new InvalidOperationException("Stored audience profile is invalid.");
        return new InventoryAudienceProfileView(
            profile.SpokenLanguages, profile.UnderstoodLanguages,
            profile.LifeStages, profile.LsmSemSegments,
            profile.TaxonomyName, profile.TaxonomyVersion, profile.Universe,
            profile.MeasurementSource, profile.MeasurementPeriod,
            profile.Methodology, profile.Limitations, profile.Measurements ?? [],
            detail.AudienceSourceLocator);
    }

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
