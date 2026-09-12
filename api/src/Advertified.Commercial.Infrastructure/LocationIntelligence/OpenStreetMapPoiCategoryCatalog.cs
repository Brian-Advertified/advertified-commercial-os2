using Advertified.Commercial.Application.LocationIntelligence;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

internal sealed record OpenStreetMapPoiCategoryDefinition(
    string Code,
    string Label,
    IReadOnlyList<KeyValuePair<string, string>> Tags);

internal static class OpenStreetMapPoiCategoryCatalog
{
    private static readonly IReadOnlyDictionary<string, OpenStreetMapPoiCategoryDefinition> Definitions =
        new Dictionary<string, OpenStreetMapPoiCategoryDefinition>(StringComparer.Ordinal)
        {
            ["PLACE_OF_WORSHIP"] = Category("PLACE_OF_WORSHIP", "Place of worship", ("amenity", "place_of_worship")),
            ["MOSQUE"] = Category("MOSQUE", "Mosque", ("amenity", "place_of_worship"), ("religion", "muslim")),
            ["HINDU_TEMPLE"] = Category("HINDU_TEMPLE", "Hindu temple", ("amenity", "place_of_worship"), ("religion", "hindu")),
            ["CHURCH"] = Category("CHURCH", "Church", ("amenity", "place_of_worship"), ("religion", "christian")),
            ["SYNAGOGUE"] = Category("SYNAGOGUE", "Synagogue", ("amenity", "place_of_worship"), ("religion", "jewish")),
            ["SHOPPING_CENTRE"] = Category("SHOPPING_CENTRE", "Shopping centre / mall", ("shop", "mall")),
            ["MARKET"] = Category("MARKET", "Market / marketplace", ("amenity", "marketplace")),
            ["SUPERMARKET"] = Category("SUPERMARKET", "Supermarket", ("shop", "supermarket")),
            ["TAXI_RANK"] = Category("TAXI_RANK", "Taxi rank / taxi stand", ("amenity", "taxi")),
            ["BUS_STATION"] = Category("BUS_STATION", "Bus station", ("amenity", "bus_station")),
            ["RAILWAY_STATION"] = Category("RAILWAY_STATION", "Railway station", ("railway", "station")),
            ["AIRPORT"] = Category("AIRPORT", "Airport / aerodrome", ("aeroway", "aerodrome")),
            ["SCHOOL"] = Category("SCHOOL", "School", ("amenity", "school")),
            ["UNIVERSITY"] = Category("UNIVERSITY", "University", ("amenity", "university")),
            ["HOSPITAL"] = Category("HOSPITAL", "Hospital", ("amenity", "hospital")),
            ["CLINIC"] = Category("CLINIC", "Clinic", ("amenity", "clinic")),
            ["PHARMACY"] = Category("PHARMACY", "Pharmacy", ("amenity", "pharmacy")),
            ["STADIUM"] = Category("STADIUM", "Stadium", ("leisure", "stadium")),
            ["PARK"] = Category("PARK", "Park", ("leisure", "park")),
            ["COMMUNITY_CENTRE"] = Category("COMMUNITY_CENTRE", "Community centre", ("amenity", "community_centre")),
            ["GOVERNMENT_OFFICE"] = Category("GOVERNMENT_OFFICE", "Government office", ("office", "government")),
        };

    internal static IReadOnlyList<LocationPoiCategoryOption> Options { get; } =
        Definitions.Values
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .Select(item => new LocationPoiCategoryOption(item.Code, item.Label))
            .ToArray();

    internal static OpenStreetMapPoiCategoryDefinition Resolve(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new InvalidOperationException("Location Intelligence requested an unsupported POI category.");

    private static OpenStreetMapPoiCategoryDefinition Category(
        string code,
        string label,
        params (string Key, string Value)[] tags) => new(
            code,
            label,
            tags.Select(item => new KeyValuePair<string, string>(item.Key, item.Value)).ToArray());
}
