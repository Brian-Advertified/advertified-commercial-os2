namespace Advertified.Commercial.Application.Inventory;

public sealed record DiscoveredPlace(
    string Id, string Name, string Address, decimal Latitude, decimal Longitude,
    string SourceLocator, string Attribution, DateTimeOffset RetrievedAtUtc,
    string GeometryBasis);

public sealed record PlaceDiscoveryResult(bool Available, IReadOnlyList<DiscoveredPlace> Places);

public interface IPlaceDiscoveryProvider
{
    Task<PlaceDiscoveryResult> SearchAsync(string query, CancellationToken cancellationToken);
}
