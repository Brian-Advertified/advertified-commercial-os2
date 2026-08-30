using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class EmailAutomationInventorySelector(
    PlanningRecordStore planningStore)
{
    public async Task<Guid[]> SelectAsync(
        TenantId tenantId,
        ActorId actorId,
        MediaMixVersionView mix,
        InventoryShortlistVersionView shortlist,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await planningStore.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var currentInventory = await planningStore.ListInventoryAsync(
            tenantId, cancellationToken);
        var inventoryByVersion = currentInventory.ToDictionary(
            item => item.ProductVersionId);
        var selected = new List<Guid>();

        foreach (var allocation in mix.Allocations.Where(item => item.BudgetMinor > 0))
        {
            var candidate = shortlist.Candidates
                .Where(item => item.IsEligible && item.Channel == allocation.Channel)
                .Where(HasUsableBenchmark)
                .Select(item => new
                {
                    Candidate = item,
                    Inventory = inventoryByVersion.GetValueOrDefault(item.ProductVersionId),
                })
                .Where(item => item.Inventory is not null &&
                    item.Inventory.RateId == item.Candidate.RateId &&
                    item.Inventory.AvailabilityId == item.Candidate.AvailabilityId)
                .Where(item => PlanSupply.Confidence(
                    new ScheduledInventory(
                        item.Inventory!, allocation.RunningPeriods), now) ==
                    MasterDataCodes.SupplyConfidenceStatuses.Confirmed)
                .OrderByDescending(item => item.Candidate.Score ?? decimal.MinValue)
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

        await transaction.CommitAsync(cancellationToken);
        return selected.Distinct().ToArray();
    }

    private static bool HasUsableBenchmark(
        InventoryShortlistCandidateView candidate) =>
        candidate.Channel is not (MasterDataCodes.Channels.Ooh or
            MasterDataCodes.Channels.Dooh) ||
        candidate.Benchmark is { Position: not null } benchmark &&
        benchmark.Position != MasterDataCodes.BenchmarkPositions.Insufficient;
}
