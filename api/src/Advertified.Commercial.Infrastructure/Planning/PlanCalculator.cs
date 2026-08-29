using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanAmounts
{
    internal static CalculatedPlanAmounts Calculate(
        PlanningBriefRow brief,
        IReadOnlyList<ScheduledInventory> scheduledInventory,
        PlanningPolicy policy)
    {
        var priced = scheduledInventory.Select(item => Price(item, policy)).ToArray();
        var subtotal = checked(priced.Sum(item => item.SupplierCostMinor));
        var totalFees = brief.FeesMinor ?? 0;
        var remainingFees = totalFees;
        var lines = new List<CalculatedLineAmounts>(priced.Length);
        for (var index = 0; index < priced.Length; index++)
        {
            var item = priced[index];
            var fees = index == priced.Length - 1
                ? remainingFees
                : Allocate(totalFees, item.SupplierCostMinor, subtotal);
            remainingFees = checked(remainingFees - fees);
            var vat = brief.VatStatus == MasterDataCodes.VatStatuses.Registered
                ? RoundMinor((item.SupplierCostMinor + fees) * policy.RegisteredVatRate)
                : 0;
            lines.Add(item with
            {
                FeesMinor = fees,
                VatMinor = vat,
                ClientPriceMinor = checked(item.SupplierCostMinor + fees + vat),
            });
        }
        var vatTotal = checked(lines.Sum(item => item.VatMinor));
        return new CalculatedPlanAmounts(
            subtotal, totalFees, vatTotal, checked(subtotal + totalFees + vatTotal), lines);
    }

    private static CalculatedLineAmounts Price(
        ScheduledInventory scheduled,
        PlanningPolicy policy)
    {
        var inventory = scheduled.Inventory;
        var quantity = MediaRatePricing.CalculateQuantity(
            inventory.RateType, scheduled.RunningPeriods, policy.RateBillingDays);
        var supplierCost = MediaRatePricing.CalculateSupplierCost(
            inventory.RateAmountMinor!.Value, inventory.RateType,
            scheduled.RunningPeriods, policy.RateBillingDays);
        return new CalculatedLineAmounts(
            inventory, scheduled.RunningPeriods, quantity, supplierCost, 0, 0, supplierCost);
    }

    private static long Allocate(long total, long value, long subtotal)
    {
        if (total == 0 || subtotal == 0)
        {
            return 0;
        }
        return RoundMinor((decimal)total * value / subtotal);
    }

    private static long RoundMinor(decimal value) => checked((long)decimal.Round(
        value, 0, MidpointRounding.AwayFromZero));
}

internal static class PlanSupply
{
    internal static string Overall(
        IReadOnlyList<ScheduledInventory> inventory,
        DateTimeOffset now)
    {
        var confidences = inventory.Select(item => Confidence(item, now)).ToArray();
        if (confidences.All(item => item == MasterDataCodes.SupplyConfidenceStatuses.Confirmed))
        {
            return MasterDataCodes.SupplyConfidenceStatuses.Confirmed;
        }
        return confidences.Any(item => item == MasterDataCodes.SupplyConfidenceStatuses.Unknown)
            ? MasterDataCodes.SupplyConfidenceStatuses.Unknown
            : MasterDataCodes.SupplyConfidenceStatuses.Indicative;
    }

    internal static string Confidence(
        ScheduledInventory scheduled,
        DateTimeOffset now)
    {
        var inventory = scheduled.Inventory;
        var latestEnd = scheduled.RunningPeriods.Max(period => period.End);
        var end = new DateTimeOffset(
            latestEnd.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        if (inventory.Availability == MasterDataCodes.AvailabilityStatuses.Available &&
            inventory.ObservedAtUtc.HasValue && inventory.ObservedAtUtc <= now &&
            inventory.ValidUntilUtc.HasValue && inventory.ValidUntilUtc >= end)
        {
            return MasterDataCodes.SupplyConfidenceStatuses.Confirmed;
        }
        return inventory.Availability == MasterDataCodes.AvailabilityStatuses.Limited
            ? MasterDataCodes.SupplyConfidenceStatuses.Indicative
            : MasterDataCodes.SupplyConfidenceStatuses.Unknown;
    }
}
