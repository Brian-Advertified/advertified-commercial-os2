using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningCommands
{
    private async Task<CommandOutcome> GenerateShortlistOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateShortlistCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadApprovedBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var mix = await store.FindLatestMixAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        if (mix is null || mix.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var inventory = await store.ListInventoryAsync(envelope.TenantId, cancellationToken);
        if (inventory.Count == 0)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = Read<MediaAllocationView[]>(mix.AllocationsJson)
            .ToDictionary(item => item.Channel, StringComparer.Ordinal);
        var geographies = Read<string[]>(brief.GeographiesJson);
        var latest = await store.FindLatestShortlistAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var versionNumber = (latest?.VersionNumber ?? 0) + 1;
        var inputHash = PlanningHash.ForShortlist(mix, inventory);
        var assumptions = new[]
        {
            "Hard eligibility runs before score calculation.",
            "Unknown supply remains eligible but is never presented as confirmed.",
        };
        var assumptionsJson = Write(assumptions);
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_shortlist_versions (
                id, tenant_id, brief_version_id, mix_version_id, version_no,
                input_hash, assumptions_json, status_code, created_by, version, created_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {briefVersionId}, {mix.Id},
                {versionNumber}, {inputHash}, {assumptionsJson}::jsonb,
                {MasterDataCodes.LifecycleStatuses.Draft}, {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
        foreach (var item in inventory)
        {
            allocations.TryGetValue(item.Channel, out var allocation);
            var eligibility = InventoryEligibilityEvaluator.Evaluate(
                item, geographies, allocations, mix.Currency, planningPolicy);
            await InsertCandidateAsync(
                envelope.TenantId, id, briefVersionId, item, allocation, eligibility,
                inventory, inputHash, now, cancellationToken);
        }
        var row = await store.FindShortlistAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The shortlist was not persisted.");
        var view = await store.BuildShortlistViewAsync(
            envelope.TenantId, row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, row.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryShortlistVersion,
            MasterDataReferences.CommercialActions.InventoryShortlistGenerated, MasterDataReferences.CommercialEventTypes.InventoryShortlistGenerated, now);
    }

    private async Task<CommandOutcome> SelectShortlistOutcomeAsync(
        Guid shortlistVersionId,
        CommandEnvelope<SelectShortlistCommand> envelope,
        CancellationToken cancellationToken)
    {
        var shortlist = await store.FindShortlistAsync(
            envelope.TenantId, shortlistVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Shortlist access denied.");
        await LoadApprovedBriefAsync(
            shortlist.BriefVersionId, envelope, cancellationToken);
        if (shortlist.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            envelope.Command.SelectedCandidateIds.Count == 0 ||
            envelope.Command.SelectedCandidateIds.Count !=
                envelope.Command.SelectedCandidateIds.Distinct().Count())
        {
            throw new InvalidLifecycleTransitionException();
        }
        var current = await store.BuildShortlistViewAsync(
            envelope.TenantId, shortlist, cancellationToken);
        var requested = envelope.Command.SelectedCandidateIds.ToHashSet();
        var eligibleIds = current.Candidates.Where(item => item.IsEligible)
            .Select(item => item.Id).ToHashSet();
        if (!requested.IsSubsetOf(eligibleIds))
        {
            throw new InvalidLifecycleTransitionException();
        }
        var now = timeProvider.GetUtcNow();
        foreach (var candidateId in eligibleIds)
        {
            await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.shortlist_selections (
                    id, tenant_id, shortlist_candidate_id, is_selected, reason,
                    selected_by, selected_at_utc)
                VALUES ({Guid.NewGuid()}, {envelope.TenantId.Value}, {candidateId},
                    {requested.Contains(candidateId)}, {envelope.Command.Reason},
                    {envelope.ActorId.Value}, {now})
                """, cancellationToken);
        }
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_shortlist_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {shortlistVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft} AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = shortlist with
        {
            Status = MasterDataCodes.LifecycleStatuses.Approved,
            Version = shortlist.Version + 1,
        };
        var view = await store.BuildShortlistViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, shortlistVersionId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryShortlistVersion,
            MasterDataReferences.CommercialActions.InventoryShortlistSelected, MasterDataReferences.CommercialEventTypes.InventoryShortlistSelected, now);
    }

    private async Task InsertCandidateAsync(
        TenantId tenantId,
        Guid shortlistId,
        Guid briefVersionId,
        PlanningInventoryRow item,
        MediaAllocationView? allocation,
        EligibilityResult eligibility,
        IReadOnlyList<PlanningInventoryRow> inventory,
        string shortlistInputHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateId = Guid.NewGuid();
        var candidateHash = PlanningHash.ForInventory(item, shortlistInputHash);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_shortlist_candidates (
                id, tenant_id, shortlist_version_id, inventory_product_id,
                product_version_id, rate_id, availability_id, is_eligible,
                rejection_reason_collection_code, rejection_reason_code, rejection_detail,
                score, rate_amount_minor, currency_code, channel_code, geography,
                input_hash, created_at_utc)
            VALUES ({candidateId}, {tenantId.Value}, {shortlistId}, {item.ProductId},
                {item.ProductVersionId}, {item.RateId}, {item.AvailabilityId},
                {eligibility.IsEligible},
                {(eligibility.RejectionReason is null
                    ? null : MasterDataCodes.RejectionReasons.Collection)},
                {eligibility.RejectionReason}, {eligibility.RejectionDetail}, {eligibility.Score},
                {item.RateAmountMinor}, {item.Currency}, {item.Channel}, {item.Geography},
                {candidateHash}, {now})
            """, cancellationToken);
        if (!eligibility.IsEligible)
        {
            return;
        }
        if (allocation is not null &&
            (item.Channel is MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh) &&
            item.RateId.HasValue)
        {
            await InsertBenchmarkAsync(
                tenantId, candidateId, item, allocation, inventory, now, cancellationToken);
        }
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.recommendation_bindings (
                id, tenant_id, brief_version_id, shortlist_version_id,
                shortlist_candidate_id, inventory_product_id, rationale, status_code)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {briefVersionId}, {shortlistId},
                {candidateId}, {item.ProductId},
                {"Eligible after governed hard constraints; score is decision support only."},
                {MasterDataCodes.LifecycleStatuses.Draft})
            """, cancellationToken);
    }

    private async Task<int> InsertBenchmarkAsync(
        TenantId tenantId,
        Guid candidateId,
        PlanningInventoryRow target,
        MediaAllocationView allocation,
        IReadOnlyList<PlanningInventoryRow> inventory,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var spatialPeers = await store.ListSpatialPeersAsync(
            tenantId, target.ProductVersionId, planningPolicy.OohRadiiKilometres[^1],
            cancellationToken);
        var result = InventoryBenchmarkCalculator.Calculate(
            target, inventory, allocation, spatialPeers, planningPolicy);
        var productsJson = Write(result.ProductVersionIds);
        var ratesJson = Write(result.RateIds);
        var distancesJson = Write(result.DistancesKilometres);
        var exclusionsJson = Write(result.Exclusions);
        var statisticsJson = Write(result.Statistics);
        return await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_benchmark_snapshots (
                id, tenant_id, shortlist_candidate_id, target_product_version_id,
                target_rate_id, policy_version, comparison_basis, geography_basis,
                cohort_product_version_ids_json, cohort_rate_ids_json, cohort_distances_json,
                exclusions_json, statistics_json, confidence, position_code, created_at_utc)
            VALUES ({result.Id}, {tenantId.Value}, {candidateId}, {target.ProductVersionId},
                {target.RateId!.Value}, {planningPolicy.BenchmarkVersion},
                {$"{target.Channel}|{target.RateType}|{target.Currency}"},
                {result.GeographyBasis}, {productsJson}::jsonb, {ratesJson}::jsonb,
                {distancesJson}::jsonb, {exclusionsJson}::jsonb, {statisticsJson}::jsonb,
                {result.Confidence}, {result.Position}, {now})
            """, cancellationToken);
    }
}

internal static partial class PlanningHash
{
    internal static string ForShortlist(
        MediaMixRow mix,
        IReadOnlyList<PlanningInventoryRow> inventory) => OpportunityCommandSupport.Hash(
            $"{mix.Id:N}|{mix.Version}|{mix.InputHash}|" + string.Join('|', inventory.Select(item =>
                $"{item.ProductVersionId:N}:{item.RateId:N}:{item.AvailabilityId:N}")));

    internal static string ForInventory(PlanningInventoryRow item, string shortlistHash) =>
        OpportunityCommandSupport.Hash(
            $"{shortlistHash}|{item.ProductVersionId:N}|{item.RateId:N}|" +
            $"{item.AvailabilityId:N}|{item.RateAmountMinor}|{item.Currency}");
}
