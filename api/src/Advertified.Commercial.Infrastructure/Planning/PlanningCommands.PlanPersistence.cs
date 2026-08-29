using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private Task<int> InsertPlanVersionAsync(
        CommandEnvelope<GenerateMediaPlanCommand> envelope,
        Guid id,
        PlanningBriefRow brief,
        MediaMixRow mix,
        ShortlistRow shortlist,
        int versionNumber,
        CalculatedPlanAmounts amounts,
        string supplyConfidence,
        string inputHash,
        string forecastJson,
        string assumptionsJson,
        string criticJson,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.media_plan_versions (
                id, tenant_id, brief_version_id, mix_version_id, shortlist_version_id,
                version_no, subtotal_minor, fees_minor, vat_minor, total_minor,
                currency_code, forecast_json, assumptions_json, supply_confidence_code,
                critic_report_json, input_hash, status_code, created_by, version, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {brief.Id}, {mix.Id}, {shortlist.Id},
                {versionNumber}, {amounts.SubtotalMinor}, {amounts.FeesMinor},
                {amounts.VatMinor}, {amounts.TotalMinor}, {brief.Currency},
                {forecastJson}::jsonb, {assumptionsJson}::jsonb, {supplyConfidence},
                {criticJson}::jsonb, {inputHash}, {MasterDataCodes.LifecycleStatuses.InReview},
                {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);

    private async Task InsertPlanLinesAsync(
        TenantId tenantId,
        Guid planId,
        InventoryShortlistCandidateView[] selected,
        CalculatedPlanAmounts amounts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < selected.Length; index++)
        {
            await InsertPlanLineAsync(
                tenantId, planId, selected[index], amounts.Lines[index], now, cancellationToken);
        }
    }

    private async Task InsertPlanLineAsync(
        TenantId tenantId,
        Guid planId,
        InventoryShortlistCandidateView candidate,
        CalculatedLineAmounts amounts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var inventory = amounts.Inventory;
        var periods = amounts.RunningPeriods.OrderBy(period => period.Start).ToArray();
        var firstStart = periods.Min(period => period.Start);
        var lastEnd = periods.Max(period => period.End);
        var periodsJson = Write(periods);
        var lineId = Guid.NewGuid();
        var confidence = PlanSupply.Confidence(
            new ScheduledInventory(inventory, periods), now);
        var forecast = Write(new
        {
            source = inventory.AvailabilitySource ?? MasterDataCodes.SupplySourceTypes.NotSupplied,
            confidence,
            uncertainty = confidence == MasterDataCodes.SupplyConfidenceStatuses.Confirmed
                ? Array.Empty<string>()
                : UnconfirmedSupplyUncertainty,
            reach = (long?)null,
            impressions = (long?)null,
        });
        var lineHash = PlanningHash.ForPlanLine(candidate, periods);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.media_plan_lines (
                id, tenant_id, plan_version_id, shortlist_candidate_id,
                inventory_product_id, product_version_id, rate_id, availability_id,
                flight_start, flight_end, running_periods_json, quantity,
                supplier_cost_minor, client_price_minor, fees_minor, vat_minor,
                forecast_json, input_hash)
            VALUES ({lineId}, {tenantId.Value}, {planId}, {candidate.Id},
                {candidate.InventoryProductId}, {candidate.ProductVersionId}, {candidate.RateId!.Value},
                {candidate.AvailabilityId}, {firstStart}, {lastEnd}, {periodsJson}::jsonb,
                {amounts.Quantity}, {amounts.SupplierCostMinor}, {amounts.ClientPriceMinor},
                {amounts.FeesMinor}, {amounts.VatMinor}, {forecast}::jsonb, {lineHash})
            """, cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.supply_coordination (
                id, tenant_id, media_plan_line_id, supplier_id, availability_code,
                rate_freshness_code, last_confirmed_at_utc, source_locator, status_code)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {lineId}, {inventory.SupplierId},
                {inventory.Availability ?? MasterDataCodes.AvailabilityStatuses.Unknown},
                {MasterDataCodes.RateFreshnessStatuses.Current},
                {(confidence == MasterDataCodes.SupplyConfidenceStatuses.Confirmed
                    ? inventory.ObservedAtUtc : null)},
                {inventory.AvailabilitySource ?? MasterDataCodes.SupplySourceTypes.NotSupplied},
                {MasterDataCodes.LifecycleStatuses.Active})
            """, cancellationToken);
    }
}

internal static partial class PlanningHash
{
    internal static string ForPlan(
        ShortlistRow shortlist,
        IReadOnlyList<InventoryShortlistCandidateView> selected,
        IEnumerable<MediaAllocationView> allocations) => OpportunityCommandSupport.Hash(
            $"{shortlist.Id:N}|{shortlist.Version}|{shortlist.InputHash}|" +
            string.Join('|', allocations.OrderBy(item => item.Channel).Select(item =>
                $"{item.Channel}:{item.BudgetMinor}:" +
                string.Join(',', item.RunningPeriods.OrderBy(period => period.Start)
                    .Select(period => $"{period.Start:O}-{period.End:O}")))) + "|" +
            string.Join('|', selected.Select(item => item.Id.ToString("N"))));

    internal static string ForPlanLine(
        InventoryShortlistCandidateView candidate,
        IReadOnlyList<MediaRunningPeriodView> periods) => OpportunityCommandSupport.Hash(
            $"{candidate.Id:N}|{candidate.ProductVersionId:N}|{candidate.RateId:N}|" +
            $"{candidate.AvailabilityId:N}|" + string.Join(',', periods.Select(period =>
                $"{period.Start:O}-{period.End:O}")));
}
