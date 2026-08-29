using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal async Task<AudienceDefinitionSetView> BuildAudienceViewAsync(
        TenantId tenantId,
        AudienceSetRow set,
        CancellationToken cancellationToken)
    {
        var rows = await DbContext.Database.SqlQuery<AudienceDefinitionRow>($"""
            SELECT id AS "Id", name AS "Name", description AS "Description",
                need_state AS "NeedState", buying_context AS "BuyingContext",
                geography_json::text AS "GeographiesJson", language AS "Language",
                life_stage AS "LifeStage", lsm_sem AS "LsmSem",
                classification_code AS "Classification", exclusions_json::text AS "ExclusionsJson",
                evidence_item_ids_json::text AS "EvidenceIdsJson",
                confidence AS "Confidence", status_code AS "Status"
            FROM commercial.audience_definitions
            WHERE tenant_id = {tenantId.Value} AND audience_set_id = {set.Id}
            ORDER BY name, id
            """).ToListAsync(cancellationToken);
        var definitions = rows.Select(row => new AudienceDefinitionView(
            row.Id, row.Name, row.Description, row.NeedState, row.BuyingContext,
            Read<string[]>(row.GeographiesJson), row.Language, row.LifeStage, row.LsmSem,
            row.Classification, Read<string[]>(row.ExclusionsJson),
            Read<Guid[]>(row.EvidenceIdsJson), row.Confidence, row.Status)).ToArray();
        return new AudienceDefinitionSetView(
            set.Id, set.BriefVersionId, set.VersionNumber, set.InputHash, set.Status,
            definitions, set.CreatedAtUtc);
    }

    internal static MediaMixVersionView BuildMixView(MediaMixRow row) => new(
        row.Id, row.BriefVersionId, row.AudienceSetId, row.VersionNumber,
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
                candidate.inventory_product_id AS "InventoryProductId",
                candidate.product_version_id AS "ProductVersionId",
                candidate.rate_id AS "RateId", candidate.availability_id AS "AvailabilityId",
                version.name AS "Name", candidate.channel_code AS "Channel",
                candidate.geography AS "Geography",
                candidate.rate_amount_minor AS "RateAmountMinor",
                candidate.currency_code AS "Currency", candidate.is_eligible AS "IsEligible",
                candidate.rejection_reason_code AS "RejectionReason",
                candidate.rejection_detail AS "RejectionDetail", candidate.score AS "Score",
                selection.is_selected AS "IsSelected", benchmark.id AS "BenchmarkId",
                benchmark.policy_version AS "BenchmarkPolicy",
                benchmark.geography_basis AS "BenchmarkGeography",
                benchmark.statistics_json::text AS "BenchmarkStatisticsJson",
                benchmark.position_code AS "BenchmarkPosition",
                benchmark.confidence AS "BenchmarkConfidence",
                benchmark.exclusions_json::text AS "BenchmarkExclusionsJson"
            FROM commercial.inventory_shortlist_candidates candidate
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = candidate.tenant_id
             AND version.id = candidate.product_version_id
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
        return new InventoryShortlistVersionView(
            shortlist.Id, shortlist.BriefVersionId, shortlist.MixVersionId,
            shortlist.VersionNumber, shortlist.InputHash, shortlist.Status,
            Read<string[]>(shortlist.AssumptionsJson), rows.Select(ToCandidateView).ToArray(),
            shortlist.Version, shortlist.CreatedAtUtc);
    }

    internal async Task<MediaPlanVersionView> BuildPlanViewAsync(
        TenantId tenantId,
        MediaPlanRow plan,
        CancellationToken cancellationToken)
    {
        var lineRows = await ListPlanLinesAsync(tenantId, plan.Id, cancellationToken);
        var lines = lineRows.Select(ToPlanLineView).ToArray();
        var resolutions = await ListResolutionsAsync(tenantId, plan.Id, cancellationToken);
        var byCode = resolutions.ToDictionary(row => row.ObjectionCode, StringComparer.Ordinal);
        var objections = Read<CriticObjection[]>(plan.CriticReportJson)
            .Select(item => ToObjectionView(item, byCode)).ToArray();
        return new MediaPlanVersionView(
            plan.Id, plan.BriefVersionId, plan.MixVersionId, plan.ShortlistVersionId,
            plan.VersionNumber, plan.SubtotalMinor, plan.FeesMinor, plan.VatMinor,
            plan.TotalMinor, plan.Currency, plan.SupplyConfidence, plan.InputHash, plan.Status,
            Read<string[]>(plan.AssumptionsJson), lines, objections, plan.CreatedBy,
            plan.ApprovedBy, plan.Version, plan.CreatedAtUtc);
    }

    private Task<List<MediaPlanLineRow>> ListPlanLinesAsync(
        TenantId tenantId,
        Guid planId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<MediaPlanLineRow>($"""
            SELECT line.id AS "Id", line.inventory_product_id AS "InventoryProductId",
                line.product_version_id AS "ProductVersionId", line.rate_id AS "RateId",
                line.availability_id AS "AvailabilityId", version.name AS "Name",
                version.channel_code AS "Channel", version.geography AS "Geography",
                line.flight_start AS "FlightStart", line.flight_end AS "FlightEnd",
                line.running_periods_json::text AS "RunningPeriodsJson",
                line.quantity AS "Quantity", line.supplier_cost_minor AS "SupplierCostMinor",
                line.client_price_minor AS "ClientPriceMinor", line.fees_minor AS "FeesMinor",
                line.vat_minor AS "VatMinor", line.forecast_json::text AS "ForecastJson",
                supply.availability_code AS "Availability",
                supply.rate_freshness_code AS "RateFreshness",
                supply.source_locator AS "SupplySource",
                supply.last_confirmed_at_utc AS "LastConfirmedAtUtc",
                plan.supply_confidence_code AS "SupplyConfidence"
            FROM commercial.media_plan_lines line
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = line.tenant_id AND plan.id = line.plan_version_id
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = line.tenant_id AND version.id = line.product_version_id
            JOIN commercial.supply_coordination supply
              ON supply.tenant_id = line.tenant_id AND supply.media_plan_line_id = line.id
            WHERE line.tenant_id = {tenantId.Value} AND line.plan_version_id = {planId}
            ORDER BY line.id
            """).ToListAsync(cancellationToken);

    private Task<List<ObjectionResolutionRow>> ListResolutionsAsync(
        TenantId tenantId,
        Guid planId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<ObjectionResolutionRow>($"""
            SELECT objection_code AS "ObjectionCode", resolution_code AS "Resolution",
                reason AS "Reason", resolved_by AS "ResolvedBy"
            FROM commercial.planning_objection_resolutions
            WHERE tenant_id = {tenantId.Value} AND plan_version_id = {planId}
            ORDER BY objection_code
            """).ToListAsync(cancellationToken);

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
            row.Id, row.InventoryProductId, row.ProductVersionId, row.RateId,
            row.AvailabilityId, row.Name, row.Channel, row.Geography, row.RateAmountMinor,
            row.Currency, row.IsEligible, row.RejectionReason, row.RejectionDetail,
            row.Score, row.IsSelected, benchmark);
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
        row.Id, row.InventoryProductId, row.ProductVersionId, row.RateId,
        row.AvailabilityId, row.Name, row.Channel, row.Geography,
        Read<MediaRunningPeriodView[]>(row.RunningPeriodsJson),
        row.Quantity, row.SupplierCostMinor, row.ClientPriceMinor,
        row.FeesMinor, row.VatMinor, row.Availability, row.RateFreshness,
        row.SupplySource, row.LastConfirmedAtUtc, row.SupplyConfidence);

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, StoredJson)
        ?? throw new InvalidOperationException("Stored planning JSON is invalid.");
}
