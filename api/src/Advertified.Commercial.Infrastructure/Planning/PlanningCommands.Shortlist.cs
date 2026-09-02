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
    private static readonly string[] ShortlistAssumptions =
    [
        "Hard eligibility is evaluated before governed suitability scoring.",
        "Inventory is planning-available unless an overlapping exception or confirmed booking conflict exists.",
        "Suitability uses the versioned OOH policy and sponsored placement never changes rank.",
    ];

    private async Task<CommandOutcome> GenerateShortlistOutcomeAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateShortlistCommand> envelope,
        CancellationToken cancellationToken)
    {
        var brief = await LoadPlanningReadyBriefAsync(
            briefVersionId, envelope, cancellationToken);
        var mix = await store.FindLatestMixAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        if (mix is null || mix.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var audienceRow = await store.FindAudienceAsync(
            envelope.TenantId, mix.AudienceSetId, cancellationToken);
        if (audienceRow is null ||
            audienceRow.Status != MasterDataCodes.LifecycleStatuses.Approved)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var audience = await store.BuildAudienceViewAsync(
            envelope.TenantId, audienceRow, cancellationToken);
        var targetIds = audience.TargetAudienceIds.ToHashSet();
        var targets = audience.Definitions.Where(item => targetIds.Contains(item.Id)).ToArray();
        if (targets.Length != targetIds.Count)
        {
            throw new InvalidOperationException("The approved target audience set is incomplete.");
        }
        var inventory = await store.ListInventoryAsync(envelope.TenantId, cancellationToken);
        if (inventory.Count == 0)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var allocations = Read<MediaAllocationView[]>(mix.AllocationsJson)
            .ToDictionary(item => item.Channel, StringComparer.Ordinal);
        var latest = await store.FindLatestShortlistAsync(
            envelope.TenantId, briefVersionId, cancellationToken);
        var id = Guid.NewGuid();
        var inputHash = PlanningHash.ForShortlist(mix, audience, inventory);
        var now = timeProvider.GetUtcNow();
        await InsertShortlistAsync(
            envelope, briefVersionId, mix.Id, id,
            (latest?.VersionNumber ?? 0) + 1, inputHash, now, cancellationToken);
        var spatialMatches = await store.EvaluateSpatialMatchesAsync(
            envelope.TenantId, briefVersionId, inventory, cancellationToken);
        var prepared = PrepareCandidates(
            inventory, allocations, Read<string[]>(brief.GeographiesJson),
            mix.Currency, targets, spatialMatches, inputHash, now);
        prepared = InventorySuitabilityScorer.Score(prepared, planningPolicy);
        prepared = await AttachBenchmarksAsync(
            envelope.TenantId, prepared, inventory, cancellationToken);
        prepared = await AttachInventoryInterpretationsAsync(
            brief, envelope, id, prepared, cancellationToken);
        await PlanningShortlistPersistence.InsertCandidatesAsync(
            store.DbContext, envelope.TenantId, id, briefVersionId,
            planningPolicy.BenchmarkVersion, now, prepared, cancellationToken);
        var row = await store.FindShortlistAsync(envelope.TenantId, id, cancellationToken)
            ?? throw new InvalidOperationException("The shortlist was not persisted.");
        var view = await store.BuildShortlistViewAsync(
            envelope.TenantId, row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, row.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryShortlistVersion,
            MasterDataReferences.CommercialActions.InventoryShortlistGenerated,
            MasterDataReferences.CommercialEventTypes.InventoryShortlistGenerated, now);
    }

    private Task<int> InsertShortlistAsync(
        CommandEnvelope<GenerateShortlistCommand> envelope,
        Guid briefVersionId,
        Guid mixId,
        Guid shortlistId,
        int versionNumber,
        string inputHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var assumptionsJson = Write(ShortlistAssumptions);
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_shortlist_versions (
                id, tenant_id, brief_version_id, mix_version_id, version_no,
                input_hash, assumptions_json, status_code, created_by, version, created_at_utc)
            VALUES ({shortlistId}, {envelope.TenantId.Value}, {briefVersionId}, {mixId},
                {versionNumber}, {inputHash}, {assumptionsJson}::jsonb,
                {MasterDataCodes.LifecycleStatuses.Draft}, {envelope.ActorId.Value}, 1, {now})
            """, cancellationToken);
    }

    private PreparedShortlistCandidate[] PrepareCandidates(
        IReadOnlyList<PlanningInventoryRow> inventory,
        Dictionary<string, MediaAllocationView> allocations,
        IReadOnlyList<string> geographies,
        string currency,
        IReadOnlyList<AudienceDefinitionView> targets,
        IReadOnlyDictionary<PlanningInventoryKey, InventorySpatialMatchView> spatialMatches,
        string shortlistInputHash,
        DateTimeOffset now) =>
        inventory.Select(item =>
        {
            allocations.TryGetValue(item.Channel, out var allocation);
            var key = new PlanningInventoryKey(
                item.InventoryTenantId, item.MarketplaceListingVersionId,
                item.ProductVersionId);
            var spatialMatch = spatialMatches[key];
            var eligibility = InventoryEligibilityEvaluator.Evaluate(
                item, geographies, allocations, currency, planningPolicy,
                spatialMatch.HasRequirements);
            eligibility = PlanningSpatialMatcher.ApplyEligibility(
                eligibility, spatialMatch);
            var audienceFit = InventoryAudienceMatcher.Evaluate(
                item.AudienceProfileJson, targets);
            eligibility = InventoryAudienceMatcher.ApplyMandatoryEligibility(
                eligibility, audienceFit);
            return new PreparedShortlistCandidate(
                Guid.NewGuid(), item, allocation, eligibility, audienceFit, spatialMatch,
                InventorySuitabilityScorer.Empty(planningPolicy),
                PlanningHash.ForInventory(item, shortlistInputHash), string.Empty, null);
        }).ToArray();

    private async Task<PreparedShortlistCandidate[]> AttachBenchmarksAsync(
        TenantId tenantId,
        PreparedShortlistCandidate[] candidates,
        IReadOnlyList<PlanningInventoryRow> inventory,
        CancellationToken cancellationToken)
    {
        var targets = candidates.Where(CanBenchmark)
            .Select(item => item.Inventory.ProductVersionId).ToArray();
        var peers = await LoadSpatialPeersAsync(tenantId, targets, cancellationToken);
        var byTarget = peers.ToLookup(item => item.TargetProductVersionId);
        return candidates.Select(candidate => !CanBenchmark(candidate)
            ? candidate
            : candidate with
            {
                Benchmark = InventoryBenchmarkCalculator.Calculate(
                    candidate.Inventory, inventory, candidate.Allocation!,
                    byTarget[candidate.Inventory.ProductVersionId].ToArray(),
                    planningPolicy),
            }).ToArray();
    }

    private async Task<PreparedShortlistCandidate[]> AttachInventoryInterpretationsAsync(
        PlanningBriefRow brief,
        CommandEnvelope<GenerateShortlistCommand> envelope,
        Guid shortlistId,
        PreparedShortlistCandidate[] candidates,
        CancellationToken cancellationToken)
    {
        var proposal = await planningAgent.InterpretInventoryAsync(
            new InventoryIntelligenceInput(
                BuildBriefInput(brief, envelope),
                shortlistId,
                1,
                candidates.Select(ToInventoryIntelligenceInput).ToArray()),
            cancellationToken);
        var interpretations = proposal.Interpretations;
        var returnedIds = interpretations.Select(item => item.CandidateId).ToArray();
        if (proposal.IncrementalCostMinor < 0 ||
            interpretations.Count != candidates.Length ||
            returnedIds.Distinct().Count() != returnedIds.Length ||
            !returnedIds.ToHashSet().SetEquals(candidates.Select(item => item.Id)))
        {
            throw new InvalidOperationException(
                "The Inventory Intelligence proposal changed the governed candidate set.");
        }
        var updated = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_shortlist_versions
            SET agent_provider_code = {proposal.Provider},
                agent_model_code = {proposal.Model},
                agent_incremental_cost_minor = {proposal.IncrementalCostMinor},
                agent_provider_request_id = {proposal.ProviderRequestId}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {shortlistId}
            """, cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                "The Inventory Intelligence usage lineage could not be persisted.");
        }
        var byCandidate = interpretations.ToDictionary(item => item.CandidateId);
        return candidates.Select(candidate => candidate with
        {
            Rationale = OpportunityCommandSupport.Required(
                byCandidate[candidate.Id].Rationale,
                1_000,
                nameof(InventoryCandidateInterpretationProposal.Rationale)),
        }).ToArray();
    }

    private InventoryIntelligenceCandidateInput ToInventoryIntelligenceInput(
        PreparedShortlistCandidate candidate)
    {
        var benchmark = candidate.Benchmark;
        return new InventoryIntelligenceCandidateInput(
            candidate.Id,
            candidate.Inventory.ProductVersionId,
            candidate.Inventory.Name,
            candidate.Inventory.Channel,
            candidate.Inventory.Geography,
            candidate.Inventory.RateAmountMinor,
            candidate.Inventory.Currency,
            candidate.Eligibility.IsEligible,
            candidate.Eligibility.RejectionReason,
            candidate.Eligibility.RejectionDetail,
            candidate.Eligibility.Score,
            candidate.AudienceFit,
            benchmark is null
                ? null
                : new InventoryBenchmarkInput(
                    planningPolicy.BenchmarkVersion,
                    benchmark.GeographyBasis,
                    benchmark.Statistics.CohortSize,
                    benchmark.Statistics.MedianMinor,
                    benchmark.Statistics.Percentile,
                    benchmark.Position,
                    benchmark.Confidence,
                    benchmark.Exclusions));
    }

    private async Task<List<PlanningSpatialPeerRow>> LoadSpatialPeersAsync(
        TenantId tenantId,
        Guid[] targets,
        CancellationToken cancellationToken)
    {
        const int batchSize = 250;
        var rows = new List<PlanningSpatialPeerRow>();
        for (var offset = 0; offset < targets.Length; offset += batchSize)
        {
            rows.AddRange(await store.ListSpatialPeersAsync(
                tenantId, targets.Skip(offset).Take(batchSize).ToArray(),
                planningPolicy.OohRadiiKilometres[^1], cancellationToken));
        }
        return rows;
    }

    private static bool CanBenchmark(PreparedShortlistCandidate candidate) =>
        candidate.Eligibility.IsEligible && candidate.Allocation is not null &&
        candidate.Inventory.RateId.HasValue && candidate.Inventory.Channel is
            MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh;

    private async Task<CommandOutcome> SelectShortlistOutcomeAsync(
        Guid shortlistVersionId,
        CommandEnvelope<SelectShortlistCommand> envelope,
        CancellationToken cancellationToken)
    {
        var shortlist = await store.FindShortlistAsync(
            envelope.TenantId, shortlistVersionId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Shortlist access denied.");
        await LoadPlanningReadyBriefAsync(
            shortlist.BriefVersionId, envelope, cancellationToken);
        EnsureSelectionRequest(shortlist, envelope.Command);
        var current = await store.BuildShortlistViewAsync(
            envelope.TenantId, shortlist, cancellationToken);
        var requested = envelope.Command.SelectedCandidateIds.ToHashSet();
        var eligibleIds = current.Candidates.Where(item => item.IsEligible)
            .Select(item => item.Id).ToHashSet();
        if (!requested.IsSubsetOf(eligibleIds))
        {
            throw new InvalidLifecycleTransitionException();
        }
        EnsureSpatialCoverage(current.Candidates.Where(item => requested.Contains(item.Id)));
        var now = timeProvider.GetUtcNow();
        await PlanningShortlistPersistence.InsertSelectionsAsync(
            store.DbContext, envelope.TenantId, eligibleIds, requested,
            NormalizeReason(envelope.Command.Reason), envelope.ActorId.Value,
            now, cancellationToken);
        await ApproveShortlistAsync(
            envelope, shortlistVersionId, now, cancellationToken);
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
            MasterDataReferences.CommercialActions.InventoryShortlistSelected,
            MasterDataReferences.CommercialEventTypes.InventoryShortlistSelected, now);
    }

    private static void EnsureSelectionRequest(
        ShortlistRow shortlist,
        SelectShortlistCommand command)
    {
        if (shortlist.Status != MasterDataCodes.LifecycleStatuses.Draft ||
            command.SelectedCandidateIds.Count == 0 ||
            command.SelectedCandidateIds.Count != command.SelectedCandidateIds.Distinct().Count())
        {
            throw new InvalidLifecycleTransitionException();
        }
    }

    private static void EnsureSpatialCoverage(
        IEnumerable<InventoryShortlistCandidateView> selected)
    {
        var candidates = selected.ToArray();
        var required = candidates.SelectMany(item =>
                item.SpatialMatch?.RequiredRequirementIds ?? [])
            .ToHashSet();
        var covered = candidates.SelectMany(item =>
                item.SpatialMatch?.MatchedRequiredRequirementIds ?? [])
            .ToHashSet();
        if (!required.IsSubsetOf(covered))
        {
            throw new InvalidLifecycleTransitionException();
        }
    }

    private async Task ApproveShortlistAsync(
        CommandEnvelope<SelectShortlistCommand> envelope,
        Guid shortlistVersionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_shortlist_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved},
                version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {shortlistVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }

    private static string? NormalizeReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= 2_000
            ? normalized
            : throw new ArgumentException("The selection reason is too long.");
    }
}

internal static partial class PlanningHash
{
    internal static string ForShortlist(
        MediaMixRow mix,
        AudienceDefinitionSetView audience,
        IReadOnlyList<PlanningInventoryRow> inventory) => OpportunityCommandSupport.Hash(
            $"{mix.Id:N}|{mix.Version}|{mix.InputHash}|{audience.Id:N}|" +
            $"{audience.VersionNumber}|{audience.InputHash}|" + string.Join('|',
                audience.Definitions.OrderBy(item => item.Id).Select(item =>
                    $"{item.Id:N}:{item.Language}:{item.LifeStage}:{item.LsmSem}:" +
                    $"{item.LsmSemTaxonomy}:{item.LsmSemTaxonomyVersion}:" +
                    $"{string.Join(',', item.EvidenceItemIds.Order())}")) + "|" +
            string.Join('|', inventory.Select(item =>
                $"{item.InventoryTenantId:N}:{item.MarketplaceListingVersionId:N}:" +
                $"{item.ProductVersionId:N}:{item.RateId:N}:{item.AvailabilityId:N}:" +
                $"{item.AudienceProfileJson}")));

    internal static string ForInventory(PlanningInventoryRow item, string shortlistHash) =>
        OpportunityCommandSupport.Hash(
            $"{shortlistHash}|{item.InventoryTenantId:N}|" +
            $"{item.MarketplaceListingVersionId:N}|{item.ProductVersionId:N}|{item.RateId:N}|" +
            $"{item.AvailabilityId:N}|{item.RateAmountMinor}|{item.Currency}|" +
            $"{item.AudienceProfileJson}");
}
