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
        PlanningPolicy policy)
    {
        if (!allocations.TryGetValue(inventory.Channel, out var allocation) ||
            allocation.BudgetMinor <= 0)
        {
            return Rejected(MasterDataCodes.RejectionReasons.IneligibleFormat,
                "The channel is not present in the approved media mix.");
        }
        if (geographies.Count == 0 || !geographies.Any(item => Matches(item, inventory.Geography)))
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
        var scheduledCost = MediaRatePricing.CalculateSupplierCost(
            inventory.RateAmountMinor.Value, inventory.RateType,
            allocation.RunningPeriods, policy.RateBillingDays);
        if (scheduledCost > allocation.BudgetMinor)
        {
            return Rejected(MasterDataCodes.RejectionReasons.BudgetMismatch,
                "The planned running periods exceed the approved channel allocation.");
        }
        if (inventory.Availability == MasterDataCodes.AvailabilityStatuses.Unavailable)
        {
            return Rejected(MasterDataCodes.RejectionReasons.Unavailable,
                "The latest published supply state is unavailable.");
        }
        return Eligible(inventory, allocation, scheduledCost, policy);
    }

    private static EligibilityResult Eligible(
        PlanningInventoryRow inventory,
        MediaAllocationView allocation,
        long scheduledCost,
        PlanningPolicy policy)
    {
        var priceRatio = (decimal)scheduledCost / allocation.BudgetMinor;
        var supplyBonus = inventory.Availability == MasterDataCodes.AvailabilityStatuses.Available
            ? policy.EligibilityAvailableSupplyBonus
            : 0m;
        var score = policy.EligibilityScoreBase - priceRatio * policy.EligibilityPriceWeight + supplyBonus;
        return new EligibilityResult(true, null, null, decimal.Round(Math.Max(0m, score), 4));
    }

    private static bool Matches(string requested, string available) =>
        available.Contains(requested, StringComparison.OrdinalIgnoreCase) ||
        requested.Contains(available, StringComparison.OrdinalIgnoreCase);

    private static EligibilityResult Rejected(string reason, string detail) =>
        new(false, reason, detail, null);
}
