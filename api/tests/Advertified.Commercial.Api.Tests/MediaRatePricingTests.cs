using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class MediaRatePricingTests
{
    private static readonly DateOnly Start = new(2026, 9, 1);
    private static readonly IReadOnlyDictionary<string, int> BillingDays =
        new Dictionary<string, int> { [MasterDataCodes.RateTypes.DayRate] = 1, [MasterDataCodes.RateTypes.WeekRate] = 7 };

    [Theory]
    [InlineData(null)]
    [InlineData("CPM")]
    [InlineData("CPC")]
    [InlineData("CPL")]
    [InlineData("CPA")]
    [InlineData("SPOT_RATE")]
    [InlineData("PACKAGE_RATE")]
    [InlineData("unknown")]
    public void MissingBuyingQuantityNeverBecomesOne(string? rateType) =>
        Assert.Throws<UnpriceableRateException>(() => MediaRatePricing.CalculateQuantity(
            rateType, [new(Start, Start.AddDays(9))], BillingDays));

    [Theory]
    [InlineData("DAY_RATE", 10)]
    [InlineData("WEEK_RATE", 2)]
    [InlineData("FLAT_RATE", 1)]
    public void GovernedFixedPeriodAndFlatRatesRetainTheirDefinedQuantity(string rateType, int expected) =>
        Assert.Equal(expected, MediaRatePricing.CalculateQuantity(rateType, [new(Start, Start.AddDays(9))], BillingDays));

    [Fact]
    public void EmptyReversedDuplicatedOrOverlappingPeriodsCannotBePriced()
    {
        IReadOnlyList<MediaRunningPeriodView>[] cases = [[], [new(Start, Start.AddDays(-1))],
            [new(Start, Start), new(Start, Start)],
            [new(Start, Start.AddDays(4)), new(Start.AddDays(3), Start.AddDays(6))]];
        foreach (var periods in cases)
            Assert.Throws<UnpriceableRateException>(() => MediaRatePricing.CalculateQuantity(
                MasterDataCodes.RateTypes.DayRate, periods, BillingDays));
    }
}
