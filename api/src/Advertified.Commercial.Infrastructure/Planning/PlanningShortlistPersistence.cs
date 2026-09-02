using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningShortlistPersistence
{
    private const int BatchSize = 250;
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static async Task InsertCandidatesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid shortlistId,
        Guid briefVersionId,
        string benchmarkPolicyVersion,
        DateTimeOffset now,
        IReadOnlyList<PreparedShortlistCandidate> candidates,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < candidates.Count; offset += BatchSize)
        {
            var batch = candidates.Skip(offset).Take(BatchSize).ToArray();
            var candidatePayload = JsonSerializer.Serialize(
                batch.Select(ToCandidatePayload), StoredJson);
            var benchmarkPayload = JsonSerializer.Serialize(
                batch.Where(item => item.Benchmark is not null)
                    .Select(item => ToBenchmarkPayload(item, benchmarkPolicyVersion)),
                StoredJson);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_shortlist_candidates (
                    id, tenant_id, shortlist_version_id, inventory_tenant_id,
                    marketplace_listing_version_id, inventory_product_id,
                    product_version_id, rate_id, availability_id, product_name, is_eligible,
                    rejection_reason_collection_code, rejection_reason_code,
                    rejection_detail, score, audience_fit_json, commercial_readiness_json,
                    supplier_commercial_json, commercial_terms_json,
                    deliverable_json, spatial_json, spatial_match_json,
                    suitability_json, logo_asset_id,
                    rate_amount_minor, currency_code,
                    channel_code, geography, input_hash, created_at_utc)
                SELECT value."id", {tenantId.Value}, {shortlistId}, value."inventoryTenantId",
                    value."listingVersionId", value."productId", value."productVersionId",
                    value."rateId", value."availabilityId", value."productName",
                    value."isEligible", value."rejectionCollection",
                    value."rejectionReason", value."rejectionDetail", value."score",
                    value."audienceFitJson"::jsonb, value."commercialReadinessJson"::jsonb,
                    value."supplierCommercialJson"::jsonb,
                    value."commercialTermsJson"::jsonb,
                    value."deliverableJson"::jsonb, value."spatialJson"::jsonb,
                    value."spatialMatchJson"::jsonb, value."suitabilityJson"::jsonb,
                    value."logoAssetId",
                    value."rateAmountMinor", value."currency", value."channel",
                    value."geography", value."inputHash", {now}
                FROM jsonb_to_recordset({candidatePayload}::jsonb) AS value(
                    "id" uuid, "recommendationId" uuid, "inventoryTenantId" uuid,
                    "listingVersionId" uuid, "productId" uuid, "productName" text,
                    "productVersionId" uuid, "rateId" uuid, "availabilityId" uuid,
                    "isEligible" boolean, "rejectionCollection" text,
                    "rejectionReason" text, "rejectionDetail" text, "score" numeric,
                    "audienceFitJson" text, "commercialReadinessJson" text,
                    "supplierCommercialJson" text, "commercialTermsJson" text,
                    "deliverableJson" text, "spatialJson" text,
                    "spatialMatchJson" text, "suitabilityJson" text, "logoAssetId" uuid,
                    "rateAmountMinor" bigint, "currency" text, "channel" text,
                    "geography" text, "inputHash" text);

                INSERT INTO commercial.recommendation_bindings (
                    id, tenant_id, brief_version_id, shortlist_version_id,
                    shortlist_candidate_id, inventory_tenant_id,
                    inventory_product_id, rationale, status_code)
                SELECT value."recommendationId", {tenantId.Value}, {briefVersionId},
                    {shortlistId}, value."id", value."inventoryTenantId", value."productId",
                    value."rationale", {MasterDataCodes.LifecycleStatuses.Draft}
                FROM jsonb_to_recordset({candidatePayload}::jsonb) AS value(
                    "id" uuid, "recommendationId" uuid, "inventoryTenantId" uuid,
                    "productId" uuid, "rationale" text,
                    "isEligible" boolean)
                WHERE value."isEligible";

                INSERT INTO commercial.inventory_benchmark_snapshots (
                    id, tenant_id, shortlist_candidate_id, inventory_tenant_id,
                    target_product_version_id,
                    target_rate_id, policy_version, comparison_basis, geography_basis,
                    cohort_product_version_ids_json, cohort_rate_ids_json,
                    cohort_distances_json, exclusions_json, statistics_json,
                    confidence, position_code, created_at_utc)
                SELECT value."id", {tenantId.Value}, value."candidateId",
                    value."inventoryTenantId",
                    value."targetProductVersionId", value."targetRateId",
                    value."policyVersion", value."comparisonBasis",
                    value."geographyBasis", value."productIdsJson"::jsonb,
                    value."rateIdsJson"::jsonb, value."distancesJson"::jsonb,
                    value."exclusionsJson"::jsonb, value."statisticsJson"::jsonb,
                    value."confidence", value."position", {now}
                FROM jsonb_to_recordset({benchmarkPayload}::jsonb) AS value(
                    "id" uuid, "candidateId" uuid, "inventoryTenantId" uuid,
                    "targetProductVersionId" uuid,
                    "targetRateId" uuid, "policyVersion" text,
                    "comparisonBasis" text, "geographyBasis" text,
                    "productIdsJson" text, "rateIdsJson" text,
                    "distancesJson" text, "exclusionsJson" text,
                    "statisticsJson" text, "confidence" numeric, "position" text);
                """, cancellationToken);
        }
    }

    internal static Task<int> InsertSelectionsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        IReadOnlyCollection<Guid> eligibleIds,
        IReadOnlySet<Guid> requestedIds,
        string? reason,
        Guid selectedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(
            eligibleIds.Select(id => new SelectionPayload(
                Guid.NewGuid(), id, requestedIds.Contains(id))), StoredJson);
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.shortlist_selections (
                id, tenant_id, shortlist_candidate_id, is_selected, reason,
                selected_by, selected_at_utc)
            SELECT value."id", {tenantId.Value}, value."candidateId",
                value."isSelected", {reason}, {selectedBy}, {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "candidateId" uuid, "isSelected" boolean)
            """, cancellationToken);
    }

    private static CandidatePayload ToCandidatePayload(
        PreparedShortlistCandidate candidate)
    {
        var inventory = candidate.Inventory;
        var eligibility = candidate.Eligibility;
        return new CandidatePayload(
            candidate.Id,
            Guid.NewGuid(),
            inventory.InventoryTenantId,
            inventory.MarketplaceListingVersionId,
            inventory.ProductId,
            inventory.Name,
            inventory.ProductVersionId,
            inventory.RateId,
            inventory.AvailabilityId,
            eligibility.IsEligible,
            eligibility.RejectionReason is null
                ? null : MasterDataCodes.RejectionReasons.Collection,
            eligibility.RejectionReason,
            eligibility.RejectionDetail,
            eligibility.Score,
            JsonSerializer.Serialize(candidate.AudienceFit, StoredJson),
            JsonSerializer.Serialize(
                InventoryCommercialReadiness.Evaluate(inventory), StoredJson),
            inventory.SupplierCommercialJson,
            inventory.CommercialTermsJson,
            inventory.DeliverableJson,
            inventory.SpatialJson,
            JsonSerializer.Serialize(candidate.SpatialMatch, StoredJson),
            JsonSerializer.Serialize(candidate.Suitability, StoredJson),
            inventory.LogoAssetId,
            inventory.RateAmountMinor,
            inventory.Currency,
            inventory.Channel,
            inventory.Geography,
            candidate.Rationale,
            candidate.InputHash);
    }

    private static BenchmarkPayload ToBenchmarkPayload(
        PreparedShortlistCandidate candidate,
        string policyVersion)
    {
        var result = candidate.Benchmark!;
        var inventory = candidate.Inventory;
        return new BenchmarkPayload(
            result.Id,
            candidate.Id,
            inventory.InventoryTenantId,
            inventory.ProductVersionId,
            inventory.RateId!.Value,
            policyVersion,
            $"{inventory.Channel}|{inventory.RateType}|{inventory.Currency}",
            result.GeographyBasis,
            JsonSerializer.Serialize(result.ProductVersionIds, StoredJson),
            JsonSerializer.Serialize(result.RateIds, StoredJson),
            JsonSerializer.Serialize(result.DistancesKilometres, StoredJson),
            JsonSerializer.Serialize(result.Exclusions, StoredJson),
            JsonSerializer.Serialize(result.Statistics, StoredJson),
            result.Confidence,
            result.Position);
    }

    private sealed record CandidatePayload(
        Guid Id,
        Guid RecommendationId,
        Guid InventoryTenantId,
        Guid? ListingVersionId,
        Guid ProductId,
        string ProductName,
        Guid ProductVersionId,
        Guid? RateId,
        Guid? AvailabilityId,
        bool IsEligible,
        string? RejectionCollection,
        string? RejectionReason,
        string? RejectionDetail,
        decimal? Score,
        string AudienceFitJson,
        string CommercialReadinessJson,
        string? SupplierCommercialJson,
        string? CommercialTermsJson,
        string? DeliverableJson,
        string? SpatialJson,
        string SpatialMatchJson,
        string SuitabilityJson,
        Guid? LogoAssetId,
        long? RateAmountMinor,
        string? Currency,
        string Channel,
        string Geography,
        string Rationale,
        string InputHash);

    private sealed record BenchmarkPayload(
        Guid Id,
        Guid CandidateId,
        Guid InventoryTenantId,
        Guid TargetProductVersionId,
        Guid TargetRateId,
        string PolicyVersion,
        string ComparisonBasis,
        string GeographyBasis,
        string ProductIdsJson,
        string RateIdsJson,
        string DistancesJson,
        string ExclusionsJson,
        string StatisticsJson,
        decimal Confidence,
        string Position);

    private sealed record SelectionPayload(
        Guid Id,
        Guid CandidateId,
        bool IsSelected);
}

internal sealed record PreparedShortlistCandidate(
    Guid Id,
    PlanningInventoryRow Inventory,
    MediaAllocationView? Allocation,
    EligibilityResult Eligibility,
    InventoryAudienceFitView AudienceFit,
    InventorySpatialMatchView SpatialMatch,
    InventorySuitabilityView Suitability,
    string InputHash,
    string Rationale,
    BenchmarkResult? Benchmark);
