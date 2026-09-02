using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Marketplace;

internal static class MarketplaceSpatialProjection
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);

    internal static string Redact(string json)
    {
        var value = JsonSerializer.Deserialize<InventorySpatialValues>(json, StoredJson)
            ?? throw new InvalidOperationException("Stored inventory spatial data is invalid.");
        var points = value.PointsOfInterest.Select(item => item with
        {
            Latitude = null,
            Longitude = null,
        }).ToArray();
        return JsonSerializer.Serialize(value with
        {
            PointsOfInterest = points,
            CoverageGeoJson = null,
            CatchmentGeoJson = null,
            RouteGeoJson = null,
            DirectionGeoJson = null,
        }, StoredJson);
    }
}
