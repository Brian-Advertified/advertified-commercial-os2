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
        PlanningPolicy policy,
        InventoryPurchaseQuantity? purchase = null)
    {
        var terms = ReadTerms(inventory.CommercialTermsJson);
        if (inventory.RateAmountMinor is null or < 0 || terms?.MinimumOrder is <= 0 ||
            terms?.ProductionCostMinor is < 0 || terms?.InstallationCostMinor is < 0 || terms?.BillingDays is <= 0)
            throw new UnpriceableRateException();
        if (!MediaRatePricing.CoversPeriods(inventory.EffectiveFrom, inventory.EffectiveTo, periods))
            throw new UnpriceableRateException();
        var (quantity, denominator) = BuyingQuantity(inventory, periods, policy, terms, purchase);
        var quoted = checked(RoundMinor((decimal)inventory.RateAmountMinor!.Value * quantity / denominator) +
            (terms?.ProductionCostMinor ?? 0) + (terms?.InstallationCostMinor ?? 0));
        var payable = inventory.SupplierVatStatus == MasterDataCodes.VatStatuses.Registered &&
            inventory.VatTreatment == MasterDataCodes.VatTreatments.Exclusive
            ? checked(quoted + RoundMinor(quoted * policy.RegisteredVatRate))
            : quoted;
        return new(quantity, payable);
    }

    private static (int Quantity, int Denominator) BuyingQuantity(PlanningInventoryRow inventory,
        IReadOnlyList<MediaRunningPeriodView> periods, PlanningPolicy policy,
        InventoryCommercialTermsValues? terms, InventoryPurchaseQuantity? purchase)
    {
        MediaRatePricing.ValidatePeriods(periods);
        if (inventory.RateType is { } rateType && policy.RateQuantityDenominators.TryGetValue(rateType, out var denominator))
        {
            if (purchase is null || purchase.InventoryTenantId != inventory.InventoryTenantId ||
                purchase.InventoryProductId != inventory.ProductId || purchase.ProductVersionId != inventory.ProductVersionId ||
                purchase.RateId != inventory.RateId || purchase.RateType != rateType || purchase.Quantity <= 0 || denominator <= 0 ||
                (purchase.Denominator.HasValue && purchase.Denominator != denominator))
                throw new UnpriceableRateException();
            if (rateType == MasterDataCodes.RateTypes.PackageRate && terms?.Inclusions?.Count is not > 0)
                throw new UnpriceableRateException();
            return (Math.Max(purchase.Quantity, terms?.MinimumOrder ?? 1), denominator);
        }
        if (purchase is not null) throw new UnpriceableRateException();
        var days = policy.RateBillingDays;
        if (inventory.RateType == MasterDataCodes.RateTypes.MonthRate)
        {
            if (terms?.BillingDays is not > 0) throw new UnpriceableRateException();
            days = new Dictionary<string, int> { [MasterDataCodes.RateTypes.MonthRate] = terms.BillingDays.Value };
        }
        return (Math.Max(MediaRatePricing.CalculateQuantity(inventory.RateType, periods, days), terms?.MinimumOrder ?? 1), 1);
    }

    private static InventoryCommercialTermsValues? ReadTerms(string? json)
    {
        try { return json is null ? null : JsonSerializer.Deserialize<InventoryCommercialTermsValues>(json, StoredJson); }
        catch (JsonException) { throw new UnpriceableRateException(); }
    }

    private static long RoundMinor(decimal value) => checked((long)decimal.Round(
        value, 0, MidpointRounding.AwayFromZero));
}

internal sealed record SupplierRateAmounts(int Quantity, long PayableMinor);
