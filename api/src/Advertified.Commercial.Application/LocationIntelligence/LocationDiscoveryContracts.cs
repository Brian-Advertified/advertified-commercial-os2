namespace Advertified.Commercial.Application.LocationIntelligence;

public sealed record LocationBounds(
    decimal South,
    decimal North,
    decimal West,
    decimal East);

public sealed record DiscoveredLocation(
    string Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string SourceLocator,
    string Attribution,
    DateTimeOffset RetrievedAtUtc,
    string GeometryBasis,
    LocationBounds? Bounds);

public sealed record PoiDiscoveryRequest(
    string PoiCategory,
    DiscoveredLocation Anchor,
    int Limit);

public sealed record DiscoveredPoi(
    string Id,
    string Name,
    string DisplayLocation,
    decimal Latitude,
    decimal Longitude,
    string SourceLocator,
    string Attribution,
    string GeometryBasis);

public interface ILocationDiscoveryProvider
{
    Task<IReadOnlyList<DiscoveredLocation>> SearchAsync(
        string query,
        CancellationToken cancellationToken);
}

public interface IPoiDiscoveryProvider
{
    Task<IReadOnlyList<DiscoveredPoi>> DiscoverAsync(
        PoiDiscoveryRequest request,
        CancellationToken cancellationToken);
}
