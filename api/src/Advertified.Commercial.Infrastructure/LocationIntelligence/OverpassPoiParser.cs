using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.LocationIntelligence;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

internal static class OverpassPoiParser
{
    internal static IReadOnlyList<DiscoveredPoi> Parse(
        string json,
        DiscoveredLocation anchor,
        int limit)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected an Overpass elements array.");

        var result = new Dictionary<string, DiscoveredPoi>(StringComparer.Ordinal);
        foreach (var item in elements.EnumerateArray())
        {
            if (result.Count >= limit)
                break;

            var type = Text(item, "type");
            if (type is not ("node" or "way" or "relation") ||
                !item.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var osmId) || osmId <= 0 ||
                !Coordinates(item, out var latitude, out var longitude) ||
                !InsideAnchor(anchor.Bounds, latitude, longitude))
                continue;

            if (!item.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object)
                continue;
            var name = Text(tags, "name") ?? Text(tags, "name:en");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var id = $"osm:{type}:{osmId}";
            result.TryAdd(id, new DiscoveredPoi(
                id,
                name,
                DisplayLocation(tags, anchor),
                latitude,
                longitude,
                $"https://www.openstreetmap.org/{type}/{osmId}",
                "© OpenStreetMap contributors · ODbL",
                type == "node"
                    ? "Mapped POI point; administrative-area membership not verified"
                    : "Mapped POI feature centre; administrative-area membership not verified"));
        }
        return result.Values.ToArray();
    }

    private static bool Coordinates(JsonElement item, out decimal latitude, out decimal longitude)
    {
        if (Coordinate(item, "lat", -90, 90, out latitude) &&
            Coordinate(item, "lon", -180, 180, out longitude))
            return true;

        if (item.TryGetProperty("center", out var center) && center.ValueKind == JsonValueKind.Object &&
            Coordinate(center, "lat", -90, 90, out latitude) &&
            Coordinate(center, "lon", -180, 180, out longitude))
            return true;

        latitude = default;
        longitude = default;
        return false;
    }

    private static bool InsideAnchor(LocationBounds? bounds, decimal latitude, decimal longitude) =>
        bounds is not null &&
        latitude >= bounds.South && latitude <= bounds.North &&
        longitude >= bounds.West && longitude <= bounds.East;

    private static string DisplayLocation(JsonElement tags, DiscoveredLocation anchor)
    {
        var parts = new[]
        {
            Text(tags, "addr:housenumber"),
            Text(tags, "addr:street"),
            Text(tags, "addr:suburb"),
            Text(tags, "addr:city"),
            Text(tags, "addr:province"),
        }.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();

        return parts.Length == 0
            ? $"Search bounds for {anchor.Name}; administrative-area membership not verified"
            : string.Join(", ", parts);
    }

    private static string? Text(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Coordinate(
        JsonElement item,
        string key,
        decimal minimum,
        decimal maximum,
        out decimal value)
    {
        value = default;
        if (!item.TryGetProperty(key, out var element))
            return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out value))
            return value >= minimum && value <= maximum;
        return element.ValueKind == JsonValueKind.String &&
            decimal.TryParse(element.GetString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
    }
}
