using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class InventoryBenchmarkReader(
    PlanningRecordStore store,
    ITenantAuthorizer authorizer,
    PlanningPolicy planningPolicy,
    TimeProvider timeProvider) : IInventoryBenchmarkReader
{
    public async Task<InventoryProductBenchmarkView> GetBenchmarkAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var target = await store.FindInventoryAsync(tenantId, productId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory access denied.");
        EnsureBenchmarkable(target);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var allocation = new MediaAllocationView(
            target.Channel,
            target.RateAmountMinor!.Value,
            string.Empty,
            [new MediaRunningPeriodView(today, today)]);
        if (!MediaRatePricing.CoversPeriods(
                target.EffectiveFrom, target.EffectiveTo, allocation.RunningPeriods))
        {
            throw new InventoryBenchmarkUnavailableException();
        }
        var inventory = await store.ListInventoryAsync(tenantId, cancellationToken);
        var spatialPeers = await store.ListSpatialPeersAsync(
            tenantId, target.ProductVersionId, planningPolicy.OohRadiiKilometres[^1],
            cancellationToken);
        var result = InventoryBenchmarkCalculator.Calculate(
            target, inventory, allocation, spatialPeers, planningPolicy);
        var view = BuildView(target, inventory, result, planningPolicy.BenchmarkVersion);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryView, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Inventory access denied.");
        }
    }

    private static void EnsureBenchmarkable(PlanningInventoryRow target)
    {
        if (target.Channel is not (MasterDataCodes.Channels.Ooh or MasterDataCodes.Channels.Dooh) ||
            !target.RateId.HasValue || !target.RateAmountMinor.HasValue ||
            string.IsNullOrWhiteSpace(target.RateType) || string.IsNullOrWhiteSpace(target.Currency))
        {
            throw new InventoryBenchmarkUnavailableException();
        }
    }

    private static InventoryProductBenchmarkView BuildView(
        PlanningInventoryRow target,
        IReadOnlyList<PlanningInventoryRow> inventory,
        BenchmarkResult result,
        string policyVersion)
    {
        var included = result.ProductVersionIds.ToHashSet();
        var comparables = inventory
            .Where(item => included.Contains(item.ProductVersionId))
            .Select(item => new InventoryComparableSiteView(
                item.ProductId,
                item.ProductVersionId,
                item.Name,
                item.Geography,
                item.RateAmountMinor!.Value,
                item.Currency!,
                result.DistancesKilometres.TryGetValue(item.ProductVersionId, out var distance)
                    ? distance
                    : null))
            .OrderBy(item => item.DistanceKilometres ?? decimal.MaxValue)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray();
        long? difference = result.Statistics.MedianMinor.HasValue
            ? target.RateAmountMinor!.Value - result.Statistics.MedianMinor.Value
            : null;
        decimal? percentage = null;
        if (difference.HasValue && result.Statistics.MedianMinor is long median && median > 0)
        {
            percentage = decimal.Round((decimal)difference.Value / median * 100m, 2);
        }
        return new InventoryProductBenchmarkView(
            target.ProductId,
            target.ProductVersionId,
            target.RateId!.Value,
            target.RateType!,
            target.RateAmountMinor!.Value,
            target.Currency!,
            policyVersion,
            result.GeographyBasis,
            result.Statistics.CohortSize,
            result.Statistics.MedianMinor,
            result.Statistics.LowerQuartileMinor,
            result.Statistics.UpperQuartileMinor,
            result.Statistics.Percentile,
            difference,
            percentage,
            result.Position,
            result.Confidence,
            comparables,
            result.Exclusions);
    }
}
