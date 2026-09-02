using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class SupplierRateCalculator
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static SupplierRateAmounts Calculate(
        PlanningInventoryRow inventory,
        IReadOnlyList<MediaRunningPeriodView> periods,
        PlanningPolicy policy)
    {
        var terms = ReadTerms(inventory.CommercialTermsJson);
        var quantity = Math.Max(
            MediaRatePricing.CalculateQuantity(
                inventory.RateType, periods, policy.RateBillingDays),
            terms?.MinimumOrder ?? 1);
        var quoted = checked(inventory.RateAmountMinor!.Value * quantity +
            (terms?.ProductionCostMinor ?? 0) + (terms?.InstallationCostMinor ?? 0));
        var payable = inventory.SupplierVatStatus == MasterDataCodes.VatStatuses.Registered &&
            inventory.VatTreatment == MasterDataCodes.VatTreatments.Exclusive
            ? checked(quoted + RoundMinor(quoted * policy.RegisteredVatRate))
            : quoted;
        return new(quantity, payable);
    }

    private static InventoryCommercialTermsValues? ReadTerms(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<InventoryCommercialTermsValues>(
            json, StoredJson);

    private static long RoundMinor(decimal value) => checked((long)decimal.Round(
        value, 0, MidpointRounding.AwayFromZero));
}

internal sealed record SupplierRateAmounts(int Quantity, long PayableMinor);
