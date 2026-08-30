using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CommercialMoneyCalculatorTests
{
    [Fact]
    public void CommissionIsCalculatedAfterDiscountAndTotalsReconcile()
    {
        var policy = Policy(
            markupBasisPoints: 2_000,
            managementFeeBasisPoints: 500,
            commissionBasisPoints: 1_000,
            vatRateBasisPoints: 1_500);

        var result = CommercialMoneyCalculator.Calculate(10_000, 1_000, policy);

        Assert.Equal(9_000, result.NetSupplierCostMinor);
        Assert.Equal(900, result.CommissionMinor);
        Assert.Equal(1_800, result.MarkupMinor);
        Assert.Equal(
            result.SubtotalMinor + result.VatMinor,
            result.TotalMinor);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegisteredVatReconcilesForExclusiveAndInclusivePrices(bool inclusive)
    {
        var policy = Policy(1_250, 375, 750, 1_500, inclusive);

        for (var supplierCost = 0L; supplierCost <= 100_000; supplierCost += 997)
        {
            var result = CommercialMoneyCalculator.Calculate(
                supplierCost, supplierCost / 11, policy);
            Assert.Equal(result.SubtotalMinor + result.VatMinor, result.TotalMinor);
            Assert.True(result.TotalMinor >= 0);
        }
    }

    [Fact]
    public void ExemptPolicyCannotClaimVatInclusivePrices()
    {
        var policy = new CommercialRatePolicy(
            0, 0, 0, MasterDataCodes.VatStatuses.Exempt, 0, true);

        Assert.Throws<ArgumentException>(() =>
            CommercialMoneyCalculator.Calculate(10_000, 0, policy));
    }

    private static CommercialRatePolicy Policy(
        int markupBasisPoints,
        int managementFeeBasisPoints,
        int commissionBasisPoints,
        int vatRateBasisPoints,
        bool inclusive = false) => new(
            markupBasisPoints,
            managementFeeBasisPoints,
            commissionBasisPoints,
            MasterDataCodes.VatStatuses.Registered,
            vatRateBasisPoints,
            inclusive);
}
