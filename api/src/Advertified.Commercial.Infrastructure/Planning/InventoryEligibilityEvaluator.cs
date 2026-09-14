using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryEligibilityEvaluator
{
    internal static EligibilityResult Evaluate(
        PlanningInventoryRow inventory,
        IReadOnlyList<string> geographies,
        IReadOnlyList<string> constraints,
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
        var briefConstraint = BriefInventoryConstraintEvaluator.Evaluate(inventory, constraints);
        if (briefConstraint is not null) return briefConstraint;
        if (!hasStructuredSpatialRequirements &&
            RequiresInventoryGeographyMatch(inventory.Channel) &&
            (geographies.Count == 0 || !MatchesAnyGeography(geographies, inventory)))
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
        if (!TryPrice(inventory, allocation, policy, out var scheduledCost))
            return Rejected(MasterDataCodes.RejectionReasons.MissingInfo,
                "The buying basis requires explicit supported quantities and valid running periods.");
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

    private static bool RequiresInventoryGeographyMatch(string channel) =>
        channel != MasterDataCodes.Channels.Social;

    private static bool MatchesAnyGeography(
        IReadOnlyList<string> requested,
        PlanningInventoryRow inventory)
    {
        var available = new List<string> { inventory.Geography, inventory.Name };
        if (!string.IsNullOrWhiteSpace(inventory.SpatialJson))
        {
            try
            {
                using var document = JsonDocument.Parse(inventory.SpatialJson);
                foreach (var field in new[] { "country", "province", "municipality", "locality", "venue", "road", "route" })
                {
                    if (document.RootElement.TryGetProperty(field, out var value) &&
                        value.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        available.Add(value.GetString()!);
                    }
                }
            }
            catch (JsonException)
            {
                // Invalid structured spatial metadata cannot create eligibility.
            }
        }
        var expandedAvailable = available
            .SelectMany(value => AudienceResearchGeographyScope.Expand([value]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return requested.Any(item => expandedAvailable.Any(value => Matches(item, value)));
    }

    private static bool Matches(string requested, string available)
    {
        if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(available))
            return false;
        if (requested.Contains("South Africa", StringComparison.OrdinalIgnoreCase))
            return true;
        var scope = NormalizeScope(requested);
        return available.Contains(scope, StringComparison.OrdinalIgnoreCase) ||
               scope.Contains(available, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeScope(string requested)
    {
        var value = requested.Trim();
        foreach (var prefix in new[] { "Broader ", "Greater " })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return value[prefix.Length..].Trim();
        }
        return value;
    }

    private static bool TryPrice(PlanningInventoryRow inventory, MediaAllocationView allocation,
        PlanningPolicy policy, out long cost)
    {
        try
        {
            cost = SupplierRateCalculator.Calculate(inventory, allocation.RunningPeriods, policy,
                InventoryPurchaseQuantities.Find(inventory, allocation)).PayableMinor;
            return true;
        }
        catch (Exception error) when (error is UnpriceableRateException or OverflowException)
        {
            cost = 0;
            return false;
        }
    }

    private static EligibilityResult Rejected(string reason, string detail) =>
        new(false, reason, detail, null);
}
