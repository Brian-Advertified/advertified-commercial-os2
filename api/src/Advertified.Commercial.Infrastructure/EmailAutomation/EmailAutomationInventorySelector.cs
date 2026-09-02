using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailAutomationInventorySelector(
    PlanningRecordStore planningStore,
    PlanningPolicy planningPolicy) : IEmailAutomationInventorySelector
{
    public async Task<Guid[]> SelectAsync(
        TenantId tenantId,
        ActorId actorId,
        MediaMixVersionView mix,
        InventoryShortlistVersionView shortlist,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        _ = now;
        if (!planningPolicy.AutomatedSelectionEnabled)
        {
            throw new EmailAutomationReviewRequiredException(
                MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                "Automated inventory selection is disabled until suitability weights are governed.");
        }
        await using var transaction = await planningStore.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var currentInventory = await planningStore.ListInventoryAsync(
            tenantId, cancellationToken);
        var inventoryByVersion = currentInventory.ToDictionary(InventoryKey.For);
        var allocations = mix.Allocations.ToDictionary(
            item => item.Channel, StringComparer.Ordinal);
        var currentEligibleIds = shortlist.Candidates
            .Where(item => item.IsEligible && AudienceEvidenceReady(item.AudienceFit))
            .Where(item => allocations.TryGetValue(item.Channel, out var allocation) &&
                CurrentAndAvailable(item, allocation, inventoryByVersion))
            .Select(item => item.Id)
            .ToHashSet();
        var selected = new List<Guid>();

        foreach (var allocation in mix.Allocations.Where(item => item.BudgetMinor > 0))
        {
            var candidate = shortlist.Candidates
                .Where(item => currentEligibleIds.Contains(item.Id) &&
                    item.Channel == allocation.Channel)
                .Select(item => new
                {
                    Candidate = item,
                    Inventory = inventoryByVersion.GetValueOrDefault(InventoryKey.For(item)),
                })
                .OrderByDescending(item => item.Candidate.Score ?? decimal.MinValue)
                .ThenBy(item =>
                    item.Candidate.Suitability?.EvidenceGaps.Count ?? int.MaxValue)
                .ThenByDescending(item => item.Inventory!.EffectiveFrom ?? DateOnly.MinValue)
                .ThenByDescending(item =>
                    item.Candidate.SpatialMatch?.MatchedRequiredRequirementIds.Count ?? 0)
                .ThenBy(item => item.Candidate.RateAmountMinor ?? long.MaxValue)
                .ThenBy(item => item.Candidate.Id)
                .FirstOrDefault();

            if (candidate is null)
            {
                throw new EmailAutomationReviewRequiredException(
                    MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                    "No confirmed eligible inventory is available for every selected media type.");
            }
            selected.Add(candidate.Candidate.Id);
        }

        AddRequiredCoverage(shortlist.Candidates, selected, currentEligibleIds);

        await transaction.CommitAsync(cancellationToken);
        return selected.Distinct().ToArray();
    }

    private static void AddRequiredCoverage(
        IReadOnlyList<InventoryShortlistCandidateView> candidates,
        List<Guid> selected,
        HashSet<Guid> currentEligibleIds)
    {
        var required = candidates.SelectMany(item =>
                item.SpatialMatch?.RequiredRequirementIds ?? [])
            .ToHashSet();
        var covered = candidates.Where(item => selected.Contains(item.Id))
            .SelectMany(item => item.SpatialMatch?.MatchedRequiredRequirementIds ?? [])
            .ToHashSet();
        while (required.Except(covered).Any())
        {
            var missing = required.Except(covered).ToHashSet();
            var additional = candidates
                .Where(item => currentEligibleIds.Contains(item.Id) &&
                    !selected.Contains(item.Id))
                .Select(item => new
                {
                    Candidate = item,
                    Coverage = (item.SpatialMatch?.MatchedRequiredRequirementIds ?? [])
                        .Count(missing.Contains),
                })
                .Where(item => item.Coverage > 0)
                .OrderByDescending(item => item.Coverage)
                .ThenByDescending(item => item.Candidate.Score ?? decimal.MinValue)
                .ThenBy(item => item.Candidate.RateAmountMinor ?? long.MaxValue)
                .ThenBy(item => item.Candidate.Id)
                .FirstOrDefault();
            if (additional is null)
            {
                throw new EmailAutomationReviewRequiredException(
                    MasterDataCodes.AutomationFailureReasons.SupplyUnready,
                    "Eligible inventory does not cover every required Brief geography.");
            }
            selected.Add(additional.Candidate.Id);
            covered.UnionWith(
                additional.Candidate.SpatialMatch?.MatchedRequiredRequirementIds ?? []);
        }
    }

    private static bool AudienceEvidenceReady(InventoryAudienceFitView fit) =>
        !fit.LsmSemMandatory || fit.LsmSemScore is > 0;

    private static bool CurrentAndAvailable(
        InventoryShortlistCandidateView candidate,
        MediaAllocationView allocation,
        IReadOnlyDictionary<InventoryKey, PlanningInventoryRow> inventoryByVersion)
    {
        var inventory = inventoryByVersion.GetValueOrDefault(InventoryKey.For(candidate));
        return inventory is not null && inventory.RateId == candidate.RateId &&
            inventory.AvailabilityId == candidate.AvailabilityId &&
            InventoryAvailabilityPolicy.IsAvailable(inventory, allocation.RunningPeriods);
    }
}
