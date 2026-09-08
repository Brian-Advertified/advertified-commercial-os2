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
            Read<string[]>(brief.ConstraintsJson), mix.Currency, targets,
            spatialMatches, inputHash, now);
        prepared = InventorySuitabilityScorer.Score(prepared, planningPolicy, targets);
        prepared = await AttachBenchmarksAsync(
            envelope.TenantId, prepared, inventory, cancellationToken);
        prepared = await AttachInventoryInterpretationsAsync(
            brief, envelope, id, prepared,
            BuildInventoryStrategy(brief, mix, audience, targets, allocations.Values),
            cancellationToken);
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
        var assumptionsJson = Write(PlanningShortlistDefaults.Assumptions);
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
        IReadOnlyList<string> constraints,
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
                item, geographies, constraints, allocations, currency, planningPolicy,
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
        InventoryStrategyInput strategy,
        CancellationToken cancellationToken)
    {
        const int maximumAgentCandidates = 5;
        var agentCandidates = candidates
            .Where(item => item.Eligibility.IsEligible)
            .OrderByDescending(item => item.Suitability.Total)
            .ThenBy(item => item.Id)
            .Take(maximumAgentCandidates)
            .ToArray();
        if (agentCandidates.Length == 0)
        {
            return candidates.Select(AttachDeterministicInterpretation).ToArray();
        }
        var proposal = await planningAgent.InterpretInventoryAsync(
            new InventoryIntelligenceInput(
                BuildBriefInput(brief, envelope),
                shortlistId,
                1,
                agentCandidates.Select(ToInventoryIntelligenceInput).ToArray(), strategy),
            cancellationToken);
        var interpretations = proposal.Interpretations;
        var returnedIds = interpretations.Select(item => item.CandidateId).ToArray();
        if (proposal.IncrementalCostMinor < 0 ||
            interpretations.Count != agentCandidates.Length ||
            returnedIds.Distinct().Count() != returnedIds.Length ||
            !returnedIds.ToHashSet().SetEquals(agentCandidates.Select(item => item.Id)))
        {
            throw new InvalidOperationException(
                "The Inventory Intelligence proposal changed the governed candidate sample.");
        }
        await PersistInventoryAgentUsageAsync(
            envelope, shortlistId, proposal, cancellationToken);
        var byCandidate = interpretations.ToDictionary(item => item.CandidateId);
        return candidates.Select(candidate =>
        {
            if (!byCandidate.TryGetValue(candidate.Id, out var interpretation))
            {
                return AttachDeterministicInterpretation(candidate);
            }
            return candidate with
            {
                Rationale = OpportunityCommandSupport.Required(
                    interpretation.Rationale,
                    1_000,
                    nameof(InventoryCandidateInterpretationProposal.Rationale)),
            };
        }).ToArray();
    }

    private async Task PersistInventoryAgentUsageAsync(
        CommandEnvelope<GenerateShortlistCommand> envelope,
        Guid shortlistId,
        InventoryIntelligenceAgentProposal proposal,
        CancellationToken cancellationToken)
    {
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
    }

    private static PreparedShortlistCandidate AttachDeterministicInterpretation(
        PreparedShortlistCandidate candidate)
    {
        if (candidate.Eligibility.IsEligible)
        {
            return candidate with
            {
                Rationale =
                    $"Eligible after governed hard constraints. Governed suitability is " +
                    $"{candidate.Suitability.Total:P0}. The visible published rate and " +
                    "benchmark remain subject to human shortlist selection.",
            };
        }
        const string prefix = "Excluded by governed hard eligibility: ";
        var detail = candidate.Eligibility.RejectionDetail ??
            candidate.Eligibility.RejectionReason ?? "The inventory item is not eligible.";
        var bounded = detail.Length <= 1_000 - prefix.Length
            ? detail
            : detail[..(1_000 - prefix.Length)];
        return candidate with { Rationale = prefix + bounded };
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
        var selected = current.Candidates
            .Where(item => requested.Contains(item.Id))
            .ToArray();
        var mix = await store.FindMixAsync(
            envelope.TenantId, shortlist.MixVersionId, cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        var requiredChannels = Read<MediaAllocationView[]>(mix.AllocationsJson)
            .Where(item => item.BudgetMinor > 0)
            .Select(item => item.Channel);
        PlanningSelectionCoverage.EnsureChannels(
            selected.Select(item => item.Channel), requiredChannels);
        PlanningSelectionCoverage.EnsureSpatial(selected);
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
