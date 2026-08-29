using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class InventoryBenchmarkCalculator
{
    internal static BenchmarkResult Calculate(
        PlanningInventoryRow target,
        IReadOnlyList<PlanningInventoryRow> inventory,
        MediaAllocationView allocation,
        IReadOnlyList<PlanningSpatialPeerRow> spatialPeers,
        PlanningPolicy policy)
    {
        var compatible = new List<PlanningInventoryRow>();
        var exclusions = new List<string>();
        foreach (var item in inventory.Where(item => item.ProductVersionId != target.ProductVersionId))
        {
            var reason = CompatibilityReason(target, item, allocation);
            if (reason is null)
            {
                compatible.Add(item);
            }
            else
            {
                exclusions.Add(Exclusion(item.ProductVersionId, reason));
            }
        }

        var cohort = ResolveLocalCohort(
            target, compatible, spatialPeers, policy, exclusions, out var geographyBasis);
        var values = cohort.Select(item => item.RateAmountMinor!.Value).Order().ToArray();
        var distanceByVersion = spatialPeers.ToDictionary(
            item => item.ProductVersionId, item => item.DistanceKilometres);
        var cohortDistances = cohort
            .Where(item => distanceByVersion.ContainsKey(item.ProductVersionId))
            .ToDictionary(item => item.ProductVersionId,
                item => distanceByVersion[item.ProductVersionId]);
        var statistics = new BenchmarkStatistics(
            values.Length,
            Quantile(values, 0.50m),
            Quantile(values, 0.25m),
            Quantile(values, 0.75m),
            Percentile(values, target.RateAmountMinor!.Value));
        return new BenchmarkResult(
            Guid.NewGuid(),
            cohort.Select(item => item.ProductVersionId).ToArray(),
            cohort.Select(item => item.RateId!.Value).ToArray(),
            cohortDistances,
            exclusions.Order(StringComparer.Ordinal).ToArray(),
            statistics,
            Confidence(values.Length, policy),
            Position(target.RateAmountMinor.Value, statistics, policy),
            geographyBasis);
    }

    private static string? CompatibilityReason(
        PlanningInventoryRow target,
        PlanningInventoryRow peer,
        MediaAllocationView allocation)
    {
        if (peer.Channel != target.Channel)
        {
            return MasterDataCodes.BenchmarkExclusionReasons.IncompatibleChannel;
        }
        if (peer.ProductType != target.ProductType)
        {
            return MasterDataCodes.BenchmarkExclusionReasons.IncompatibleProductType;
        }
        if (peer.RateType != target.RateType)
        {
            return MasterDataCodes.BenchmarkExclusionReasons.IncompatibleRateBasis;
        }
        if (peer.Currency != target.Currency)
        {
            return MasterDataCodes.BenchmarkExclusionReasons.IncompatibleCurrency;
        }
        if (!peer.RateId.HasValue || !peer.RateAmountMinor.HasValue)
        {
            return MasterDataCodes.BenchmarkExclusionReasons.MissingRate;
        }
        return MediaRatePricing.CoversPeriods(
            peer.EffectiveFrom, peer.EffectiveTo, allocation.RunningPeriods)
            ? null
            : MasterDataCodes.BenchmarkExclusionReasons.RatePeriodMismatch;
    }

    private static PlanningInventoryRow[] ResolveLocalCohort(
        PlanningInventoryRow target,
        IReadOnlyList<PlanningInventoryRow> compatible,
        IReadOnlyList<PlanningSpatialPeerRow> spatialPeers,
        PlanningPolicy policy,
        List<string> exclusions,
        out string geographyBasis)
    {
        if (!HasCoordinates(target))
        {
            geographyBasis = $"GEOGRAPHY:{target.Geography}";
            return compatible.Where(item => string.Equals(
                target.Geography.Trim(), item.Geography.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var distanceByVersion = spatialPeers.ToDictionary(
            item => item.ProductVersionId, item => item.DistanceKilometres);
        AddSpatialExclusions(compatible, distanceByVersion, exclusions);
        PlanningInventoryRow[] peers = [];
        var radius = policy.OohRadiiKilometres[^1];
        foreach (var candidateRadius in policy.OohRadiiKilometres)
        {
            radius = candidateRadius;
            peers = compatible.Where(item =>
                distanceByVersion.TryGetValue(item.ProductVersionId, out var distance) &&
                distance <= candidateRadius).ToArray();
            if (peers.Length >= policy.MinimumBenchmarkCohort)
            {
                break;
            }
        }
        geographyBasis = $"RADIUS_{radius:0.##}_KM";
        return peers;
    }

    private static void AddSpatialExclusions(
        IReadOnlyList<PlanningInventoryRow> compatible,
        Dictionary<Guid, decimal> distanceByVersion,
        List<string> exclusions)
    {
        foreach (var item in compatible)
        {
            if (!HasCoordinates(item))
            {
                exclusions.Add(Exclusion(
                    item.ProductVersionId,
                    MasterDataCodes.BenchmarkExclusionReasons.MissingCoordinates));
            }
            else if (!distanceByVersion.ContainsKey(item.ProductVersionId))
            {
                exclusions.Add(Exclusion(
                    item.ProductVersionId,
                    MasterDataCodes.BenchmarkExclusionReasons.OutsideComparisonArea));
            }
        }
    }

    private static bool HasCoordinates(PlanningInventoryRow item) =>
        item.Latitude.HasValue && item.Longitude.HasValue;

    private static long? Quantile(long[] values, decimal quantile)
    {
        if (values.Length == 0)
        {
            return null;
        }
        var position = (values.Length - 1) * quantile;
        var lower = (int)decimal.Floor(position);
        var upper = (int)decimal.Ceiling(position);
        if (lower == upper)
        {
            return values[lower];
        }
        var fraction = position - lower;
        return checked((long)decimal.Round(
            values[lower] + (values[upper] - values[lower]) * fraction,
            0, MidpointRounding.AwayFromZero));
    }

    private static decimal? Percentile(long[] values, long target)
    {
        if (values.Length == 0)
        {
            return null;
        }
        return decimal.Round((decimal)values.Count(value => value <= target) /
            values.Length * 100m, 2);
    }

    private static decimal Confidence(int cohortSize, PlanningPolicy policy)
    {
        if (cohortSize >= policy.HighBenchmarkCohort)
        {
            return policy.HighBenchmarkConfidence;
        }
        if (cohortSize >= policy.MediumBenchmarkCohort)
        {
            return policy.MediumBenchmarkConfidence;
        }
        return cohortSize >= policy.MinimumBenchmarkCohort
            ? policy.LowBenchmarkConfidence
            : 0m;
    }

    private static string Position(
        long target,
        BenchmarkStatistics statistics,
        PlanningPolicy policy)
    {
        if (statistics.CohortSize < policy.MinimumBenchmarkCohort)
        {
            return MasterDataCodes.BenchmarkPositions.Insufficient;
        }
        if (target <= statistics.LowerQuartileMinor)
        {
            return MasterDataCodes.BenchmarkPositions.StrongValue;
        }
        return target <= statistics.UpperQuartileMinor
            ? MasterDataCodes.BenchmarkPositions.MarketAligned
            : MasterDataCodes.BenchmarkPositions.AboveMarket;
    }

    private static string Exclusion(Guid productVersionId, string reason) =>
        $"{productVersionId:N}:{reason}";
}
