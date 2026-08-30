using Advertified.Commercial.Application.Planning;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class MediaRatePricing
{
    internal static int CalculateQuantity(
        string? rateType,
        IReadOnlyList<MediaRunningPeriodView> periods,
        IReadOnlyDictionary<string, int> billingDays) =>
        Quantity(rateType, periods, billingDays);

    internal static int Quantity(
        string? rateType,
        IReadOnlyList<MediaRunningPeriodView> periods,
        IReadOnlyDictionary<string, int> billingDays)
    {
        if (rateType is null || !billingDays.TryGetValue(rateType, out var daysPerUnit))
        {
            return 1;
        }
        var units = periods.Sum(period => Units(period.Start, period.End, daysPerUnit));
        return Math.Max(1, units);
    }

    internal static bool Covers(
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        IReadOnlyList<MediaRunningPeriodView> periods) =>
        periods.All(period =>
            (!effectiveFrom.HasValue || effectiveFrom.Value <= period.Start) &&
            (!effectiveTo.HasValue || effectiveTo.Value >= period.End));

    internal static bool CoversPeriods(
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        IReadOnlyList<MediaRunningPeriodView> periods) =>
        Covers(effectiveFrom, effectiveTo, periods);

    internal static long CalculateSupplierCost(
        long amountMinor,
        string? rateType,
        IReadOnlyList<MediaRunningPeriodView> periods,
        IReadOnlyDictionary<string, int> billingDays) =>
        checked(amountMinor * CalculateQuantity(rateType, periods, billingDays));

    private static int Units(DateOnly start, DateOnly end, int daysPerUnit)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        return (int)Math.Ceiling((decimal)days / daysPerUnit);
    }
}
