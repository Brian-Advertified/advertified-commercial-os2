using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static CampaignModeSelectionView BuildCampaignModeView(
        CampaignModeRow row,
        CampaignModePolicy policy) => new(
            row.Id,
            row.BriefVersionId,
            row.Mode,
            policy.AllowedChannels(row.Mode),
            true,
            row.DecisionSource,
            row.Confidence,
            row.Reason,
            row.SelectedBy,
            row.SelectedAtUtc);

    internal static AudienceStrategyView BuildAudienceView(
        AudienceArtifactRow set)
    {
        var artifact = JsonSerializer.Deserialize<AudienceStrategyArtifact>(set.ArtifactJson, StoredJson)
            ?? throw new InvalidOperationException("Stored Audience Intelligence JSON is invalid.");
        var definitions = artifact.Segments.Select(item => new AudienceSegmentView(
            item.Id, item.Name, item.Description, item.NeedState, item.BuyingContext,
            item.Geographies, item.Language, item.LifeStage, item.LsmSem,
            item.LsmSemTaxonomy, item.LsmSemTaxonomyVersion,
            item.Classification, item.Exclusions, item.EvidenceItemIds,
            item.ReferenceObservationIds, item.Confidence, set.Status, item.LsmSemMandatory)).ToArray();
        return new AudienceStrategyView(
            set.Id, set.BriefVersionId, set.VersionNumber,
            artifact.TargetAudienceIds, artifact.TargetingRationale,
            artifact.PositioningStatement, set.InputHash, set.Status,
            definitions, set.CreatedBy, set.ApprovedBy, set.Version,
            set.ApprovedAtUtc, set.CreatedAtUtc);
    }

    internal static MediaMixVersionView BuildMixView(MediaMixRow row) => new(
        row.Id, row.BriefVersionId, row.AudienceArtifactId, row.MediaStrategyArtifactId, row.VersionNumber,
        row.TotalBudgetMinor, row.Currency,
        Read<MediaAllocationView[]>(row.AllocationsJson),
        Read<string[]>(row.AssumptionsJson), row.InputHash, row.Status,
        row.CreatedBy, row.ApprovedBy, row.Version, row.CreatedAtUtc);

    internal async Task<InventoryShortlistVersionView> BuildShortlistViewAsync(
        TenantId tenantId,
        ShortlistRow shortlist,
        CancellationToken cancellationToken)
    {
        var rows = await DbContext.Database.SqlQuery<ShortlistCandidateRow>($"""
            SELECT candidate.id AS "Id",
                candidate.inventory_tenant_id AS "InventoryTenantId",
                candidate.supplier_id AS "SupplierId",
                candidate.supplier_name AS "SupplierName",
                candidate.latitude AS "Latitude", candidate.longitude AS "Longitude",
                candidate.marketplace_listing_version_id AS "MarketplaceListingVersionId",
                candidate.inventory_product_id AS "InventoryProductId",
                candidate.product_version_id AS "ProductVersionId",
                candidate.rate_id AS "RateId", candidate.availability_id AS "AvailabilityId",
                candidate.product_name AS "Name", candidate.channel_code AS "Channel",
                candidate.geography AS "Geography",
                candidate.rate_amount_minor AS "RateAmountMinor",
                candidate.currency_code AS "Currency", candidate.is_eligible AS "IsEligible",
                candidate.rejection_reason_code AS "RejectionReason",
                candidate.rejection_detail AS "RejectionDetail", candidate.score AS "Score",
                candidate.audience_fit_json::text AS "AudienceFitJson",
                candidate.commercial_readiness_json::text AS "CommercialReadinessJson",
                candidate.supplier_commercial_json::text AS "SupplierCommercialJson",
                candidate.commercial_terms_json::text AS "CommercialTermsJson",
                candidate.deliverable_json::text AS "DeliverableJson",
                candidate.spatial_json::text AS "SpatialJson",
                candidate.spatial_match_json::text AS "SpatialMatchJson",
                candidate.suitability_json::text AS "SuitabilityJson",
                candidate.logo_asset_id AS "LogoAssetId",
                recommendation.rationale AS "Rationale",
                selection.is_selected AS "IsSelected", benchmark.id AS "BenchmarkId",
                benchmark.policy_version AS "BenchmarkPolicy",
                benchmark.geography_basis AS "BenchmarkGeography",
                benchmark.statistics_json::text AS "BenchmarkStatisticsJson",
                benchmark.position_code AS "BenchmarkPosition",
                benchmark.confidence AS "BenchmarkConfidence",
                benchmark.exclusions_json::text AS "BenchmarkExclusionsJson"
            FROM commercial.inventory_shortlist_candidates candidate
            LEFT JOIN commercial.recommendation_bindings recommendation
              ON recommendation.tenant_id = candidate.tenant_id
             AND recommendation.shortlist_candidate_id = candidate.id
            LEFT JOIN commercial.shortlist_selections selection
              ON selection.tenant_id = candidate.tenant_id
             AND selection.shortlist_candidate_id = candidate.id
            LEFT JOIN commercial.inventory_benchmark_snapshots benchmark
              ON benchmark.tenant_id = candidate.tenant_id
             AND benchmark.shortlist_candidate_id = candidate.id
            WHERE candidate.tenant_id = {tenantId.Value}
              AND candidate.shortlist_version_id = {shortlist.Id}
            ORDER BY candidate.is_eligible DESC, candidate.score DESC NULLS LAST, candidate.id
            """).ToListAsync(cancellationToken);
        var candidates = rows.Select(ToCandidateView).ToArray();
        var mix = await FindMixAsync(tenantId, shortlist.MixVersionId, cancellationToken)
            ?? throw new InvalidOperationException("The shortlist media mix is unavailable.");
        var commercialPolicy = await new CommercialPolicyRecordStore(dbContext)
            .FindCurrentAsync(tenantId, cancellationToken);
        var combinations = CampaignCombinationAssessment.Evaluate(
            candidates, BuildMixView(mix), commercialPolicy);
        var research = await PlanningResearchPortfolioReader.ReadAsync(
            dbContext, tenantId, cancellationToken);
        var forecasted = CampaignAudienceForecast.Attach(
            combinations.Alternatives, candidates, research, tenantId);
        combinations = combinations with
        {
            Alternatives = CampaignScenarioClassifier.Attach(forecasted),
        };
        return new InventoryShortlistVersionView(
            shortlist.Id, shortlist.BriefVersionId, shortlist.MixVersionId,
            shortlist.VersionNumber, shortlist.InputHash, shortlist.Status,
            Read<string[]>(shortlist.AssumptionsJson), candidates,
            shortlist.Version, shortlist.CreatedAtUtc, combinations);
    }

    internal async Task<MediaPlanVersionView> BuildPlanViewAsync(
        TenantId tenantId,
        MediaPlanRow plan,
        CancellationToken cancellationToken)
    {
        var views = await BuildPlanViewsAsync(
            tenantId, [plan], cancellationToken);
        return views[0];
    }

    internal async Task<IReadOnlyList<MediaPlanVersionView>> BuildPlanViewsAsync(
        TenantId tenantId,
        IReadOnlyList<MediaPlanRow> plans,
        CancellationToken cancellationToken)
    {
        if (plans.Count == 0) return [];
        var planIds = plans.Select(item => item.Id).ToArray();
        var lineRows = await ListPlanLinesAsync(tenantId, planIds, cancellationToken);
        var resolutionRows = await ListResolutionsAsync(
            tenantId, planIds, cancellationToken);
        var lines = lineRows.ToLookup(item => item.PlanVersionId);
        var resolutions = resolutionRows.ToLookup(item => item.PlanVersionId);
        return plans.Select(plan => BuildPlanView(
            plan, lines[plan.Id], resolutions[plan.Id])).ToArray();
    }

    private Task<List<MediaPlanLineRow>> ListPlanLinesAsync(
        TenantId tenantId,
        Guid[] planIds,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<MediaPlanLineRow>($"""
            SELECT line.plan_version_id AS "PlanVersionId", line.id AS "Id",
                line.inventory_tenant_id AS "InventoryTenantId",
                line.supplier_name AS "SupplierName",
                line.marketplace_listing_version_id AS "MarketplaceListingVersionId",
                line.inventory_product_id AS "InventoryProductId",
                line.product_version_id AS "ProductVersionId", line.rate_id AS "RateId",
                line.availability_id AS "AvailabilityId", line.product_name AS "Name",
                line.channel_code AS "Channel", line.geography AS "Geography",
                line.flight_start AS "FlightStart", line.flight_end AS "FlightEnd",
                line.running_periods_json::text AS "RunningPeriodsJson",
                line.quantity AS "Quantity", line.supplier_cost_minor AS "SupplierCostMinor",
                line.client_price_minor AS "ClientPriceMinor", line.fees_minor AS "FeesMinor",
                line.vat_minor AS "VatMinor", line.forecast_json::text AS "ForecastJson",
                supply.availability_code AS "Availability",
                supply.rate_freshness_code AS "RateFreshness",
                supply.source_locator AS "SupplySource",
                supply.last_confirmed_at_utc AS "LastConfirmedAtUtc",
                plan.supply_confidence_code AS "SupplyConfidence",
                line.supplier_commercial_json::text AS "SupplierCommercialJson",
                line.commercial_terms_json::text AS "CommercialTermsJson",
                line.deliverable_json::text AS "DeliverableJson",
                line.spatial_json::text AS "SpatialJson",
                line.logo_asset_id AS "LogoAssetId",
                line.purchase_json::text AS "PurchaseJson"
            FROM commercial.media_plan_lines line
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = line.tenant_id AND plan.id = line.plan_version_id
            JOIN commercial.supply_coordination supply
              ON supply.tenant_id = line.tenant_id AND supply.media_plan_line_id = line.id
            WHERE line.tenant_id = {tenantId.Value}
              AND line.plan_version_id = ANY({planIds})
            ORDER BY line.plan_version_id, line.id
            """).ToListAsync(cancellationToken);

    private Task<List<ObjectionResolutionRow>> ListResolutionsAsync(
        TenantId tenantId,
        Guid[] planIds,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<ObjectionResolutionRow>($"""
            SELECT plan_version_id AS "PlanVersionId", objection_code AS "ObjectionCode",
                resolution_code AS "Resolution", reason AS "Reason",
                resolved_by AS "ResolvedBy"
            FROM commercial.planning_objection_resolutions
            WHERE tenant_id = {tenantId.Value} AND plan_version_id = ANY({planIds})
            ORDER BY plan_version_id, objection_code
            """).ToListAsync(cancellationToken);

    private static MediaPlanVersionView BuildPlanView(
        MediaPlanRow plan,
        IEnumerable<MediaPlanLineRow> lineRows,
        IEnumerable<ObjectionResolutionRow> resolutionRows)
    {
        var lines = lineRows.Select(ToPlanLineView).ToArray();
        var resolutions = resolutionRows.ToDictionary(
            row => row.ObjectionCode, StringComparer.Ordinal);
        var objections = Read<CriticObjection[]>(plan.CriticReportJson)
            .Select(item => ToObjectionView(item, resolutions)).ToArray();
        return new MediaPlanVersionView(
            plan.Id, plan.BriefVersionId, plan.MixVersionId, plan.ShortlistVersionId,
            plan.VersionNumber, plan.FeesMinor, plan.VatMinor,
            plan.TotalMinor, plan.Currency, plan.SupplyConfidence, plan.InputHash, plan.Status,
            Read<string[]>(plan.AssumptionsJson), lines, objections, plan.CreatedBy,
            plan.ApprovedBy, plan.Version, plan.CreatedAtUtc,
            plan.CommercialPolicyVersionId);
    }

    private static InventoryShortlistCandidateView ToCandidateView(ShortlistCandidateRow row)
    {
        InventoryBenchmarkView? benchmark = null;
        if (row.BenchmarkId.HasValue && row.BenchmarkStatisticsJson is not null)
        {
            var stats = Read<BenchmarkStatistics>(row.BenchmarkStatisticsJson);
            benchmark = new InventoryBenchmarkView(
                row.BenchmarkId.Value, row.BenchmarkPolicy!, row.BenchmarkGeography!,
                stats.CohortSize, stats.MedianMinor, stats.LowerQuartileMinor,
                stats.UpperQuartileMinor, stats.Percentile, row.BenchmarkPosition!,
                row.BenchmarkConfidence!.Value,
                Read<string[]>(row.BenchmarkExclusionsJson ?? "[]"));
        }
        return new InventoryShortlistCandidateView(
            row.Id, row.InventoryTenantId, row.SupplierId, row.SupplierName,
            row.Latitude, row.Longitude, row.MarketplaceListingVersionId,
            row.InventoryProductId, row.ProductVersionId, row.RateId, row.AvailabilityId,
            row.Name, row.Channel, row.Geography, row.RateAmountMinor, row.Currency,
            row.IsEligible, row.RejectionReason, row.RejectionDetail, row.Score,
            Read<InventoryAudienceFitView>(row.AudienceFitJson),
            row.Rationale, row.IsSelected, benchmark, row.LogoAssetId,
            Read<InventoryCommercialReadinessView>(row.CommercialReadinessJson),
            ReadOptional<Advertified.Commercial.Application.Inventory.InventorySupplierCommercialValues>(
                row.SupplierCommercialJson),
            ReadOptional<Advertified.Commercial.Application.Inventory.InventoryCommercialTermsValues>(
                row.CommercialTermsJson),
            ReadOptional<Advertified.Commercial.Application.Inventory.InventoryDeliverableValues>(
                row.DeliverableJson),
            ReadOptional<Advertified.Commercial.Application.Inventory.InventorySpatialValues>(
                row.SpatialJson),
            Read<InventorySpatialMatchView>(row.SpatialMatchJson),
            Read<InventorySuitabilityView>(row.SuitabilityJson));
    }

    private static PlanObjectionView ToObjectionView(
        CriticObjection objection,
        Dictionary<string, ObjectionResolutionRow> resolutions)
    {
        resolutions.TryGetValue(objection.Code, out var resolution);
        return new PlanObjectionView(
            objection.Code, objection.Severity, objection.AffectedField,
            objection.EvidenceGap, objection.RecommendedResolution,
            resolution?.Resolution, resolution?.Reason, resolution?.ResolvedBy);
    }

    private static MediaPlanLineView ToPlanLineView(MediaPlanLineRow row) => new(
        row.Id, row.InventoryTenantId, row.SupplierName, row.MarketplaceListingVersionId,
        row.InventoryProductId, row.ProductVersionId, row.RateId, row.AvailabilityId,
        row.Name, row.Channel, row.Geography,
        Read<MediaRunningPeriodView[]>(row.RunningPeriodsJson),
        row.Quantity, row.ClientPriceMinor,
        row.FeesMinor, row.VatMinor, row.Availability, row.RateFreshness,
        row.SupplySource, row.LastConfirmedAtUtc, row.SupplyConfidence,
        ReadOptional<Advertified.Commercial.Application.Inventory.InventorySupplierCommercialValues>(
            row.SupplierCommercialJson),
        ReadOptional<Advertified.Commercial.Application.Inventory.InventoryCommercialTermsValues>(
            row.CommercialTermsJson),
        ReadOptional<Advertified.Commercial.Application.Inventory.InventoryDeliverableValues>(
            row.DeliverableJson),
        ReadOptional<Advertified.Commercial.Application.Inventory.InventorySpatialValues>(
            row.SpatialJson),
        row.LogoAssetId, ReadOptional<InventoryPurchaseQuantity>(row.PurchaseJson));

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored planning JSON is invalid.");

    private static T? ReadOptional<T>(string? json) where T : class =>
        json is null ? null : Read<T>(json);
}
