using System.Runtime.CompilerServices;
using System.Text.Json;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed partial class InventoryResearchStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<Guid> InsertDatasetAsync(
        TenantId tenantId, RegisterInventoryResearchDatasetCommand command,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var datasetId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_research_datasets (
                id, tenant_id, source_name, dataset_name, measurement_period,
                methodology, universe, rights_reference, taxonomy_name,
                taxonomy_version, limitations, status_code, created_by, created_at_utc, version)
            VALUES ({datasetId}, {tenantId.Value}, {command.SourceName.Trim()}, {command.DatasetName.Trim()},
                {command.MeasurementPeriod.Trim()}, {command.Methodology.Trim()}, {command.Universe.Trim()},
                {command.RightsReference.Trim()}, {command.TaxonomyName}, {command.TaxonomyVersion},
                {command.Limitations}, {MasterDataCodes.LifecycleStatuses.InReview}, {actorId}, {now}, 1)
            """, cancellationToken);
        foreach (var observation in command.Observations)
            await InsertObservationAndMatchAsync(
                tenantId, datasetId, observation, actorId, now, cancellationToken);
        foreach (var portfolio in command.Portfolios ?? [])
            await InsertPortfolioAsync(tenantId, datasetId, portfolio, cancellationToken);
        return datasetId;
    }

    private async Task InsertObservationAndMatchAsync(
        TenantId tenantId, Guid datasetId, InventoryResearchObservationInput observation,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var observationId = Guid.NewGuid();
        var profileJson = JsonSerializer.Serialize(observation.AudienceProfile, InventoryRowMapper.StoredJson);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_research_observations (
                id, tenant_id, dataset_id, source_locator, channel_code, supplier_id,
                supplier_product_code, outlet_id, geography, audience_profile_json, created_at_utc)
            VALUES ({observationId}, {tenantId.Value}, {datasetId}, {observation.SourceLocator.Trim()},
                {observation.Channel}, {observation.SupplierId}, {observation.SupplierProductCode},
                {observation.OutletId}, {observation.Geography}, {profileJson}::jsonb, {now})
            """, cancellationToken);
        var candidates = await ExactCandidatesAsync(tenantId, observation, cancellationToken);
        var exact = candidates.Count == 1 ? candidates[0] : null;
        Guid? productId = exact?.ProductId;
        Guid? productVersionId = exact?.ProductVersionId;
        var productName = exact?.ProductName;
        var basis = exact is null ? null : MatchBasis(exact, observation);
        var detail = candidates.Count switch
        {
            0 => "No current inventory version matches the supplied canonical identity.",
            1 => "One current inventory version matches the supplied canonical identity.",
            _ => $"{candidates.Count} current inventory versions match; automatic linkage is blocked as ambiguous.",
        };
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_research_matches (
                id, tenant_id, dataset_id, observation_id, product_id, product_version_id,
                product_name, match_basis, match_detail, status_code, created_by, created_at_utc, version)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {datasetId}, {observationId},
                {productId}, {productVersionId}, {productName}, {basis}, {detail},
                {MasterDataCodes.LifecycleStatuses.InReview}, {actorId}, {now}, 1)
            """, cancellationToken);
    }

    private Task<int> InsertPortfolioAsync(
        TenantId tenantId, Guid datasetId, InventoryResearchPortfolioInput portfolio,
        CancellationToken cancellationToken)
    {
        var locators = JsonSerializer.Serialize(
            portfolio.ObservationSourceLocators.Select(item => item.Trim()).Order(StringComparer.Ordinal).ToArray(),
            InventoryRowMapper.StoredJson);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_research_portfolios (
                id, tenant_id, dataset_id, source_locator, observation_source_locators_json,
                deduplicated_reach, unit_code, status_code)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {datasetId}, {portfolio.SourceLocator.Trim()},
                {locators}::jsonb, {portfolio.DeduplicatedReach}, {portfolio.Unit},
                {MasterDataCodes.LifecycleStatuses.InReview})
            """, cancellationToken);
    }

    private Task<List<InventoryResearchCandidateRow>> ExactCandidatesAsync(
        TenantId tenantId, InventoryResearchObservationInput observation,
        CancellationToken cancellationToken) => dbContext.Database.SqlQuery<InventoryResearchCandidateRow>($"""
            SELECT product.id AS "ProductId", version.id AS "ProductVersionId",
                version.name AS "ProductName", version.outlet_id AS "OutletId",
                product.supplier_id AS "SupplierId", product.supplier_product_code AS "SupplierProductCode"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            WHERE product.tenant_id = {tenantId.Value}
              AND product.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND (({observation.OutletId}::text IS NOT NULL AND version.outlet_id = {observation.OutletId})
                OR ({observation.SupplierId}::uuid IS NOT NULL AND product.supplier_id = {observation.SupplierId}
                  AND product.supplier_product_code = {observation.SupplierProductCode}))
              AND ({observation.Channel}::text IS NULL OR version.channel_code = {observation.Channel})
              AND ({observation.Geography}::text IS NULL OR
                   lower(btrim(version.geography)) = lower(btrim({observation.Geography})))
            ORDER BY product.id
            LIMIT 3
            """).ToListAsync(cancellationToken);

    internal Task<InventoryResearchDatasetRow?> FindDatasetAsync(
        TenantId tenantId, Guid datasetId, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryResearchDatasetRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", source_name AS "SourceName",
                dataset_name AS "DatasetName", measurement_period AS "MeasurementPeriod",
                methodology AS "Methodology", universe AS "Universe", rights_reference AS "RightsReference",
                taxonomy_name AS "TaxonomyName", taxonomy_version AS "TaxonomyVersion",
                limitations AS "Limitations", status_code AS "Status", created_by AS "CreatedBy",
                created_at_utc AS "CreatedAtUtc", version AS "Version"
            FROM commercial.inventory_research_datasets
            WHERE tenant_id = {tenantId.Value} AND id = {datasetId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InventoryResearchMatchRow?> FindMatchAsync(
        TenantId tenantId, Guid matchId, bool forUpdate,
        CancellationToken cancellationToken)
    {
        var suffix = forUpdate ? " FOR UPDATE OF match" : string.Empty;
        return dbContext.Database.SqlQuery<InventoryResearchMatchRow>(
            FormattableStringFactory.Create(
                """
                SELECT match.id AS "Id", match.dataset_id AS "DatasetId",
                    match.observation_id AS "ObservationId", observation.source_locator AS "SourceLocator",
                    match.product_id AS "ProductId", match.product_version_id AS "ProductVersionId",
                    match.product_name AS "ProductName", match.match_basis AS "MatchBasis",
                    match.match_detail AS "MatchDetail", observation.audience_profile_json::text AS "AudienceProfileJson",
                    match.status_code AS "Status", match.created_by AS "CreatedBy",
                    match.reviewed_by AS "ReviewedBy", match.reviewed_at_utc AS "ReviewedAtUtc",
                    match.review_reason AS "ReviewReason", match.applied_product_version_id AS "AppliedProductVersionId",
                    match.version AS "Version"
                FROM commercial.inventory_research_matches match
                JOIN commercial.inventory_research_observations observation
                  ON observation.tenant_id = match.tenant_id AND observation.id = match.observation_id
                WHERE match.tenant_id = {0} AND match.id = {1}
                """ + suffix, tenantId.Value, matchId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<InventoryResearchMatchRow>> ListMatchesAsync(
        TenantId tenantId, Guid datasetId, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryResearchMatchRow>($"""
            SELECT match.id AS "Id", match.dataset_id AS "DatasetId",
                match.observation_id AS "ObservationId", observation.source_locator AS "SourceLocator",
                match.product_id AS "ProductId", match.product_version_id AS "ProductVersionId",
                match.product_name AS "ProductName", match.match_basis AS "MatchBasis",
                match.match_detail AS "MatchDetail", observation.audience_profile_json::text AS "AudienceProfileJson",
                match.status_code AS "Status", match.created_by AS "CreatedBy",
                match.reviewed_by AS "ReviewedBy", match.reviewed_at_utc AS "ReviewedAtUtc",
                match.review_reason AS "ReviewReason", match.applied_product_version_id AS "AppliedProductVersionId",
                match.version AS "Version"
            FROM commercial.inventory_research_matches match
            JOIN commercial.inventory_research_observations observation
              ON observation.tenant_id = match.tenant_id AND observation.id = match.observation_id
            WHERE match.tenant_id = {tenantId.Value} AND match.dataset_id = {datasetId}
            ORDER BY observation.source_locator, match.id
            """).ToListAsync(cancellationToken);

    internal Task<List<InventoryResearchPortfolioRow>> ListPortfoliosAsync(
        TenantId tenantId, Guid datasetId, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryResearchPortfolioRow>($"""
            SELECT id AS "Id", source_locator AS "SourceLocator",
                observation_source_locators_json::text AS "ObservationSourceLocatorsJson",
                deduplicated_reach AS "DeduplicatedReach", unit_code AS "Unit",
                product_version_ids_json::text AS "ProductVersionIdsJson", status_code AS "Status"
            FROM commercial.inventory_research_portfolios
            WHERE tenant_id = {tenantId.Value} AND dataset_id = {datasetId}
            ORDER BY source_locator, id
            """).ToListAsync(cancellationToken);

    internal async Task<Guid> ApplyAudienceProfileAsync(
        TenantId tenantId, InventoryResearchMatchRow match,
        Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!match.ProductId.HasValue || !match.ProductVersionId.HasValue)
            throw new InvalidLifecycleTransitionException();
        var product = await LockCurrentProductAsync(
            tenantId, match.ProductId.Value, cancellationToken);
        if (product is null || product.CurrentVersionId != match.ProductVersionId.Value)
            throw new VersionConflictException();
        var newVersionId = Guid.NewGuid();
        await CloneProductVersionAsync(
            tenantId, product, match, newVersionId, actorId, now, cancellationToken);
        await CloneVersionDependenciesAsync(
            tenantId, product.CurrentVersionId, newVersionId, cancellationToken);
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_products
            SET current_version_id = {newVersionId}, version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {product.ProductId}
              AND current_version_id = {product.CurrentVersionId} AND version = {product.Version}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await RefreshPublishedMarketplaceAudienceAsync(
            tenantId, product.ProductId, product.CurrentVersionId, newVersionId,
            match.AudienceProfileJson, actorId, now, cancellationToken);
        return newVersionId;
    }

    private Task<InventoryResearchProductRow?> LockCurrentProductAsync(
        TenantId tenantId, Guid productId, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryResearchProductRow>($"""
            SELECT product.id AS "ProductId", product.current_version_id AS "CurrentVersionId",
                product.version AS "Version", version.version_number AS "VersionNumber"
            FROM commercial.inventory_products product
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = product.tenant_id AND version.id = product.current_version_id
            WHERE product.tenant_id = {tenantId.Value} AND product.id = {productId}
            FOR UPDATE OF product
            """).SingleOrDefaultAsync(cancellationToken);

    private Task<int> CloneProductVersionAsync(
        TenantId tenantId, InventoryResearchProductRow product, InventoryResearchMatchRow match,
        Guid newVersionId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_product_versions (
                id, tenant_id, product_id, version_number, name, channel_code, product_type_code,
                geography, address, latitude, longitude, extension_json, verification_code,
                source_import_id, source_candidate_id, published_by, published_at_utc,
                audience_profile_json, description, deliverable_json, spatial_json,
                coverage_geometry, catchment_geometry, route_geometry, direction_geometry,
                inventory_release_id, outlet_id, outlet_name, outlet_identity_basis, outlet_source_locator)
            SELECT {newVersionId}, version.tenant_id, version.product_id, {product.VersionNumber + 1},
                version.name, version.channel_code, version.product_type_code, version.geography,
                version.address, version.latitude, version.longitude, version.extension_json,
                version.verification_code, version.source_import_id, version.source_candidate_id,
                {actorId}, {now}, {match.AudienceProfileJson}::jsonb, version.description,
                version.deliverable_json, version.spatial_json, version.coverage_geometry,
                version.catchment_geometry, version.route_geometry, version.direction_geometry,
                version.inventory_release_id, version.outlet_id, version.outlet_name,
                version.outlet_identity_basis, version.outlet_source_locator
            FROM commercial.inventory_product_versions version
            WHERE version.tenant_id = {tenantId.Value} AND version.id = {product.CurrentVersionId}
            """, cancellationToken);

    private async Task CloneVersionDependenciesAsync(
        TenantId tenantId, Guid oldVersionId, Guid newVersionId,
        CancellationToken cancellationToken)
    {
        await CloneRatesAsync(tenantId, oldVersionId, newVersionId, cancellationToken);
        await CloneAvailabilityAsync(tenantId, oldVersionId, newVersionId, cancellationToken);
        await CloneAssetsAsync(tenantId, oldVersionId, newVersionId, cancellationToken);
        await ClonePoisAsync(tenantId, oldVersionId, newVersionId, cancellationToken);
        await CloneAvailabilityExceptionsAsync(tenantId, oldVersionId, newVersionId, cancellationToken);
    }

    private Task<int> CloneRatesAsync(TenantId tenantId, Guid oldId, Guid newId, CancellationToken token) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_rates (id, tenant_id, product_version_id, rate_type_code,
                currency_code, amount_minor, effective_from, effective_to, source_locator,
                vat_treatment_code, commercial_terms_json)
            SELECT gen_random_uuid(), tenant_id, {newId}, rate_type_code, currency_code, amount_minor,
                effective_from, effective_to, source_locator, vat_treatment_code, commercial_terms_json
            FROM commercial.inventory_rates WHERE tenant_id = {tenantId.Value} AND product_version_id = {oldId}
            """, token);

    private Task<int> CloneAvailabilityAsync(TenantId tenantId, Guid oldId, Guid newId, CancellationToken token) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_availability (id, tenant_id, product_version_id,
                availability_code, observed_at_utc, valid_until_utc, source_locator)
            SELECT gen_random_uuid(), tenant_id, {newId}, availability_code, observed_at_utc,
                valid_until_utc, source_locator FROM commercial.inventory_availability
            WHERE tenant_id = {tenantId.Value} AND product_version_id = {oldId}
            """, token);

    private Task<int> CloneAssetsAsync(TenantId tenantId, Guid oldId, Guid newId, CancellationToken token) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_assets (id, tenant_id, product_version_id, asset_type_code,
                object_key, content_hash, media_type, source_import_id)
            SELECT gen_random_uuid(), tenant_id, {newId}, asset_type_code, object_key, content_hash,
                media_type, source_import_id FROM commercial.inventory_assets
            WHERE tenant_id = {tenantId.Value} AND product_version_id = {oldId}
            """, token);

    private Task<int> ClonePoisAsync(TenantId tenantId, Guid oldId, Guid newId, CancellationToken token) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_product_points_of_interest (
                id, tenant_id, product_version_id, name, category, location, source_import_id)
            SELECT gen_random_uuid(), tenant_id, {newId}, name, category, location, source_import_id
            FROM commercial.inventory_product_points_of_interest
            WHERE tenant_id = {tenantId.Value} AND product_version_id = {oldId}
            """, token);

    private Task<int> CloneAvailabilityExceptionsAsync(
        TenantId tenantId, Guid oldId, Guid newId, CancellationToken token) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_availability_exceptions (
                id, tenant_id, product_id, product_version_id, exception_type_code,
                starts_on, ends_on, source_locator, evidence_hash, recorded_by, recorded_at_utc)
            SELECT gen_random_uuid(), tenant_id, product_id, {newId}, exception_type_code,
                starts_on, ends_on, source_locator, evidence_hash, recorded_by, recorded_at_utc
            FROM commercial.inventory_availability_exceptions
            WHERE tenant_id = {tenantId.Value} AND product_version_id = {oldId}
            """, token);

    internal async Task UpdateMatchReviewAsync(
        TenantId tenantId, InventoryResearchMatchRow match, string decision, string reason,
        Guid actorId, DateTimeOffset now, Guid? appliedVersionId, CancellationToken cancellationToken)
    {
        var status = decision == MasterDataCodes.InventoryReviewDecisions.Approve
            ? MasterDataCodes.LifecycleStatuses.Completed : MasterDataCodes.LifecycleStatuses.Rejected;
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_research_matches
            SET status_code = {status}, reviewed_by = {actorId}, reviewed_at_utc = {now},
                review_reason = {reason}, applied_product_version_id = {appliedVersionId}, version = version + 1
            WHERE tenant_id = {tenantId.Value} AND id = {match.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.InReview} AND version = {match.Version}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        var pending = await dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM commercial.inventory_research_matches
                WHERE tenant_id = {tenantId.Value} AND dataset_id = {match.DatasetId}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.InReview}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!pending)
        {
            await FinalizePortfoliosAsync(tenantId, match.DatasetId, cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_research_datasets
                SET status_code = {MasterDataCodes.LifecycleStatuses.Completed}, version = version + 1
                WHERE tenant_id = {tenantId.Value} AND id = {match.DatasetId}
                  AND status_code = {MasterDataCodes.LifecycleStatuses.InReview}
                """, cancellationToken);
        }
    }

    private Task<int> FinalizePortfoliosAsync(
        TenantId tenantId, Guid datasetId, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_research_portfolios portfolio
            SET product_version_ids_json = CASE WHEN mapped.all_applied THEN mapped.product_versions ELSE NULL END,
                status_code = CASE WHEN mapped.all_applied
                    THEN {MasterDataCodes.LifecycleStatuses.Completed}
                    ELSE {MasterDataCodes.LifecycleStatuses.Rejected} END
            FROM (
                SELECT source.id,
                    bool_and(match.status_code = {MasterDataCodes.LifecycleStatuses.Completed}
                        AND match.applied_product_version_id IS NOT NULL) AS all_applied,
                    jsonb_agg(match.applied_product_version_id ORDER BY match.applied_product_version_id)
                        FILTER (WHERE match.applied_product_version_id IS NOT NULL) AS product_versions
                FROM commercial.inventory_research_portfolios source
                CROSS JOIN LATERAL jsonb_array_elements_text(
                    source.observation_source_locators_json) locator(value)
                JOIN commercial.inventory_research_observations observation
                  ON observation.tenant_id = source.tenant_id
                 AND observation.dataset_id = source.dataset_id
                 AND observation.source_locator = locator.value
                JOIN commercial.inventory_research_matches match
                  ON match.tenant_id = observation.tenant_id AND match.observation_id = observation.id
                WHERE source.tenant_id = {tenantId.Value} AND source.dataset_id = {datasetId}
                  AND source.status_code = {MasterDataCodes.LifecycleStatuses.InReview}
                GROUP BY source.id
            ) mapped
            WHERE portfolio.tenant_id = {tenantId.Value} AND portfolio.dataset_id = {datasetId}
              AND portfolio.status_code = {MasterDataCodes.LifecycleStatuses.InReview}
              AND portfolio.id = mapped.id
            """, cancellationToken);

    private static string MatchBasis(
        InventoryResearchCandidateRow candidate, InventoryResearchObservationInput observation)
    {
        var outlet = !string.IsNullOrWhiteSpace(observation.OutletId) &&
            string.Equals(candidate.OutletId, observation.OutletId, StringComparison.OrdinalIgnoreCase);
        var product = observation.SupplierId == candidate.SupplierId &&
            string.Equals(candidate.SupplierProductCode, observation.SupplierProductCode, StringComparison.Ordinal);
        return outlet && product ? "CANONICAL_OUTLET_AND_PRODUCT" :
            outlet ? "CANONICAL_OUTLET_ID" : "SUPPLIER_PRODUCT_CODE";
    }
}
