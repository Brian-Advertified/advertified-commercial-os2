using System.Globalization;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class NominatimPlaceParser
{
    internal static IReadOnlyList<DiscoveredPlace> Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected a place-result array.");
        var result = new List<DiscoveredPlace>();
        foreach (var item in document.RootElement.EnumerateArray().Take(10))
        {
            var type = Text(item, "osm_type");
            if (type is not ("node" or "way" or "relation") ||
                !item.TryGetProperty("osm_id", out var id) || !id.TryGetInt64(out var osmId) || osmId <= 0 ||
                !Coordinate(item, "lat", -90, 90, out var latitude) ||
                !Coordinate(item, "lon", -180, 180, out var longitude) ||
                !item.TryGetProperty("address", out var address) || Text(address, "country_code") != "za") continue;
            var name = Text(item, "name");
            var display = Text(item, "display_name");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(display)) continue;
            result.Add(new DiscoveredPlace($"osm:{type}:{osmId}", name, display, latitude, longitude,
                $"https://www.openstreetmap.org/{type}/{osmId}",
                "© OpenStreetMap contributors · ODbL", now,
                type == "node" ? "Mapped point; verify the exact branch" : "Mapped feature centre; verify the exact branch"));
        }
        return result;
    }

    private static string? Text(JsonElement item, string key) => item.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Coordinate(JsonElement item, string key, decimal minimum, decimal maximum,
        out decimal value) => decimal.TryParse(Text(item, key), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value) && value >= minimum && value <= maximum;
}
