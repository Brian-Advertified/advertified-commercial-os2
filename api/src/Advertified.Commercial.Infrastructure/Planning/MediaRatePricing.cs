using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class MediaRatePricing
{
    internal static int CalculateQuantity(
        string? rateType,
        IReadOnlyList<MediaRunningPeriodView> periods,
        IReadOnlyDictionary<string, int> billingDays)
    {
        if (periods.Count == 0)
        {
            throw new ArgumentException("At least one media running period is required.");
        }
        if (rateType is null || !billingDays.TryGetValue(rateType, out var daysPerUnit))
        {
            return 1;
        }
        var units = periods.Sum(period =>
        {
            var days = period.End.DayNumber - period.Start.DayNumber + 1;
            return (int)Math.Ceiling((decimal)days / daysPerUnit);
        });
        return Math.Max(1, units);
    }

    internal static long CalculateSupplierCost(
        long rateAmountMinor,
        string? rateType,
        IReadOnlyList<MediaRunningPeriodView> periods,
        IReadOnlyDictionary<string, int> billingDays) =>
        checked(rateAmountMinor * CalculateQuantity(rateType, periods, billingDays));

    internal static bool CoversPeriods(
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        IReadOnlyList<MediaRunningPeriodView> periods) =>
        periods.All(period =>
            (!effectiveFrom.HasValue || effectiveFrom.Value <= period.Start) &&
            (!effectiveTo.HasValue || effectiveTo.Value >= period.End));
}
