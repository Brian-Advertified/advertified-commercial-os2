using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryEligibilityEvaluator
{
    internal static EligibilityResult Evaluate(
        PlanningInventoryRow inventory,
        IReadOnlyList<string> geographies,
        IReadOnlyDictionary<string, MediaAllocationView> allocations,
        string currency,
        PlanningPolicy policy,
        bool hasStructuredSpatialRequirements = false)
    {
        if (!allocations.TryGetValue(inventory.Channel, out var allocation) ||
            allocation.BudgetMinor <= 0)
        {
            return Rejected(MasterDataCodes.RejectionReasons.IneligibleFormat,
                "The channel is not present in the approved media mix.");
        }
        if (!hasStructuredSpatialRequirements &&
            (geographies.Count == 0 ||
             !geographies.Any(item => Matches(item, inventory.Geography))))
        {
            return Rejected(MasterDataCodes.RejectionReasons.IneligibleGeography,
                "The product geography does not match the approved Brief.");
        }
        if (!inventory.RateId.HasValue || !inventory.RateAmountMinor.HasValue ||
            string.IsNullOrWhiteSpace(inventory.Currency))
        {
            return Rejected(MasterDataCodes.RejectionReasons.MissingInfo,
                "A published rate is not available.");
        }
        if (!string.Equals(inventory.Currency, currency, StringComparison.Ordinal))
        {
            return Rejected(MasterDataCodes.RejectionReasons.MissingInfo,
                "The rate currency is not supported by the mix.");
        }
        if (!MediaRatePricing.CoversPeriods(
                inventory.EffectiveFrom, inventory.EffectiveTo, allocation.RunningPeriods))
        {
            return Rejected(MasterDataCodes.RejectionReasons.StaleRate,
                "The published rate does not cover the planned running periods.");
        }
        var scheduledCost = SupplierRateCalculator.Calculate(
            inventory, allocation.RunningPeriods, policy).PayableMinor;
        if (scheduledCost > allocation.BudgetMinor)
        {
            return Rejected(MasterDataCodes.RejectionReasons.BudgetMismatch,
                "The planned running periods exceed the approved channel allocation.");
        }
        if (!InventoryAvailabilityPolicy.IsAvailable(inventory, allocation.RunningPeriods))
        {
            return Rejected(MasterDataCodes.RejectionReasons.Unavailable,
                "The product is unavailable for at least one planned running period.");
        }
        return new EligibilityResult(true, null, null, null);
    }

    private static bool Matches(string requested, string available) =>
        available.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
        requested.Contains(available, StringComparison.OrdinalIgnoreCase);

    private static EligibilityResult Rejected(string reason, string detail) =>
        new(false, reason, detail, null);
}
