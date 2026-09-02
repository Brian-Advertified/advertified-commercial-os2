using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanAmounts
{
    internal static CalculatedPlanAmounts Calculate(
        IReadOnlyList<ScheduledInventory> scheduledInventory,
        CommercialPolicyRow commercialPolicy,
        PlanningPolicy policy)
    {
        var priced = scheduledInventory.Select(item => Price(item, policy)).ToArray();
        var clientPolicy = new CommercialRatePolicy(
            commercialPolicy.MarkupBasisPoints,
            commercialPolicy.ManagementFeeBasisPoints,
            commercialPolicy.CommissionBasisPoints,
            commercialPolicy.VatStatus,
            commercialPolicy.VatRateBasisPoints,
            commercialPolicy.PricesIncludeVat);
        var lines = priced.Select(item => ApplyClientPolicy(item, clientPolicy)).ToArray();
        var totalFees = checked(lines.Sum(item => item.FeesMinor));
        var vatTotal = checked(lines.Sum(item => item.VatMinor));
        var total = checked(lines.Sum(item => item.ClientPriceMinor));
        return new CalculatedPlanAmounts(
            checked(total - totalFees - vatTotal), totalFees, vatTotal, total, lines);
    }

    private static CalculatedLineAmounts Price(
        ScheduledInventory scheduled,
        PlanningPolicy policy)
    {
        var inventory = scheduled.Inventory;
        var supplier = SupplierRateCalculator.Calculate(
            inventory, scheduled.RunningPeriods, policy);
        return new CalculatedLineAmounts(
            inventory, scheduled.RunningPeriods, supplier.Quantity,
            supplier.PayableMinor, 0, 0, supplier.PayableMinor);
    }

    private static CalculatedLineAmounts ApplyClientPolicy(
        CalculatedLineAmounts item,
        CommercialRatePolicy policy)
    {
        var money = CommercialMoneyCalculator.Calculate(item.SupplierCostMinor, 0, policy);
        var fees = checked(money.MarkupMinor + money.CommissionMinor +
            money.ManagementFeeMinor);
        return item with
        {
            FeesMinor = fees,
            VatMinor = money.VatMinor,
            ClientPriceMinor = money.TotalMinor,
        };
    }

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
        _ = now;
        return !InventoryAvailabilityPolicy.IsAvailable(
                scheduled.Inventory, scheduled.RunningPeriods)
            ? MasterDataCodes.SupplyConfidenceStatuses.Unknown
            : MasterDataCodes.SupplyConfidenceStatuses.Confirmed;
    }
}
