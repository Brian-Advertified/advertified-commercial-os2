using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.LocationIntelligence;

namespace Advertified.Commercial.Infrastructure.LocationIntelligence;

internal static class NominatimLocationParser
{
    internal static IReadOnlyList<DiscoveredLocation> Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected a location-result array.");

        var result = new List<DiscoveredLocation>();
        foreach (var item in document.RootElement.EnumerateArray().Take(10))
        {
            var type = Text(item, "osm_type");
            if (type is not ("node" or "way" or "relation") ||
                !item.TryGetProperty("osm_id", out var id) || !id.TryGetInt64(out var osmId) || osmId <= 0 ||
                !Coordinate(item, "lat", -90, 90, out var latitude) ||
                !Coordinate(item, "lon", -180, 180, out var longitude) ||
                !item.TryGetProperty("address", out var address) || Text(address, "country_code") != "za")
                continue;

            var name = Text(item, "name");
            var display = Text(item, "display_name");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(display))
                continue;

            result.Add(new DiscoveredLocation(
                $"osm:{type}:{osmId}",
                name,
                display,
                latitude,
                longitude,
                $"https://www.openstreetmap.org/{type}/{osmId}",
                "© OpenStreetMap contributors · ODbL",
                now,
                type == "node"
                    ? "Mapped point; verify the exact place before activation"
                    : "Mapped feature centre; verify the exact place before activation",
                Bounds(item)));
        }
        return result;
    }

    private static LocationBounds? Bounds(JsonElement item)
    {
        if (!item.TryGetProperty("boundingbox", out var box) ||
            box.ValueKind != JsonValueKind.Array || box.GetArrayLength() != 4)
            return null;

        var values = box.EnumerateArray().ToArray();
        if (!Coordinate(values[0], -90, 90, out var south) ||
            !Coordinate(values[1], -90, 90, out var north) ||
            !Coordinate(values[2], -180, 180, out var west) ||
            !Coordinate(values[3], -180, 180, out var east) ||
            south > north || west > east)
            return null;

        return new LocationBounds(south, north, west, east);
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
        return item.TryGetProperty(key, out var element) &&
               Coordinate(element, minimum, maximum, out value);
    }

    private static bool Coordinate(
        JsonElement element,
        decimal minimum,
        decimal maximum,
        out decimal value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.String &&
               decimal.TryParse(element.GetString(), NumberStyles.Float,
                   CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
    }
}
