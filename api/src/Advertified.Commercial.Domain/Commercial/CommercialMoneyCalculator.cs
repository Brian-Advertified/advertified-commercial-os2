using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Domain.Commercial;

public sealed record CommercialRatePolicy(
    int MarkupBasisPoints,
    int ManagementFeeBasisPoints,
    int CommissionBasisPoints,
    string VatStatus,
    int VatRateBasisPoints,
    bool PricesIncludeVat);

public sealed record CommercialMoneyBreakdown(
    long SupplierCostMinor,
    long DiscountMinor,
    long NetSupplierCostMinor,
    long MarkupMinor,
    long CommissionMinor,
    long ManagementFeeMinor,
    long SubtotalMinor,
    long VatMinor,
    long TotalMinor);

public static class CommercialMoneyCalculator
{
    public static CommercialMoneyBreakdown Calculate(
        long supplierCostMinor,
        long discountMinor,
        CommercialRatePolicy policy)
    {
        Validate(supplierCostMinor, discountMinor, policy);
        var netSupplier = checked(supplierCostMinor - discountMinor);
        var markup = ApplyBasisPoints(netSupplier, policy.MarkupBasisPoints);
        var commission = ApplyBasisPoints(netSupplier, policy.CommissionBasisPoints);
        var feeBase = checked(netSupplier + markup + commission);
        var managementFee = ApplyBasisPoints(feeBase, policy.ManagementFeeBasisPoints);
        var gross = checked(feeBase + managementFee);
        var (subtotal, vat, total) = CalculateVat(gross, policy);
        return new CommercialMoneyBreakdown(
            supplierCostMinor,
            discountMinor,
            netSupplier,
            markup,
            commission,
            managementFee,
            subtotal,
            vat,
            total);
    }

    private static (long Subtotal, long Vat, long Total) CalculateVat(
        long gross,
        CommercialRatePolicy policy)
    {
        if (policy.VatStatus != MasterDataCodes.VatStatuses.Registered)
        {
            return (gross, 0, gross);
        }
        if (policy.PricesIncludeVat)
        {
            var vat = DivideAndRound(gross, policy.VatRateBasisPoints,
                10_000 + policy.VatRateBasisPoints);
            return (gross - vat, vat, gross);
        }
        var exclusiveVat = ApplyBasisPoints(gross, policy.VatRateBasisPoints);
        return (gross, exclusiveVat, checked(gross + exclusiveVat));
    }

    private static long ApplyBasisPoints(long amountMinor, int basisPoints) =>
        DivideAndRound(amountMinor, basisPoints, 10_000);

    private static long DivideAndRound(long amount, int numerator, int denominator) =>
        checked((long)decimal.Round(
            (decimal)amount * numerator / denominator,
            0,
            MidpointRounding.AwayFromZero));

    private static void Validate(
        long supplierCostMinor,
        long discountMinor,
        CommercialRatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (supplierCostMinor < 0 || discountMinor < 0 || discountMinor > supplierCostMinor ||
            policy.MarkupBasisPoints is < 0 or > 100_000 ||
            policy.ManagementFeeBasisPoints is < 0 or > 100_000 ||
            policy.CommissionBasisPoints is < 0 or > 10_000 ||
            policy.VatRateBasisPoints is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }
        var registered = policy.VatStatus == MasterDataCodes.VatStatuses.Registered;
        var exempt = policy.VatStatus is MasterDataCodes.VatStatuses.Exempt
            or MasterDataCodes.VatStatuses.NotApplicable;
        if (!registered && !exempt || registered && policy.VatRateBasisPoints == 0 ||
            exempt && (policy.VatRateBasisPoints != 0 || policy.PricesIncludeVat))
        {
            throw new ArgumentException("The VAT treatment is inconsistent.", nameof(policy));
        }
    }
}
