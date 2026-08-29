using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Planning;

public sealed record InventoryComparableSiteView(
    Guid ProductId,
    Guid ProductVersionId,
    string Name,
    string Geography,
    long RateAmountMinor,
    string Currency,
    decimal? DistanceKilometres);

public sealed record InventoryProductBenchmarkView(
    Guid ProductId,
    Guid ProductVersionId,
    Guid RateId,
    string RateType,
    long RateAmountMinor,
    string Currency,
    string PolicyVersion,
    string GeographyBasis,
    int CohortSize,
    long? MedianMinor,
    long? LowerQuartileMinor,
    long? UpperQuartileMinor,
    decimal? Percentile,
    long? DifferenceFromMedianMinor,
    decimal? DifferenceFromMedianPercent,
    string Position,
    decimal Confidence,
    IReadOnlyList<InventoryComparableSiteView> Comparables,
    IReadOnlyList<string> Exclusions);

public sealed class InventoryBenchmarkUnavailableException : Exception
{
    public InventoryBenchmarkUnavailableException() : base("Inventory benchmark is unavailable.")
    {
    }
}

public interface IInventoryBenchmarkReader
{
    Task<InventoryProductBenchmarkView> GetBenchmarkAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken);
}
