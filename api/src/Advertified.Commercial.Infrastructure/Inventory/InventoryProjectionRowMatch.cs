using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryProjectionRowMatch
{
    // Local extraction protocol marker; it is never a supplier/commercial fact.
    internal const string AmbiguityField = "projectionmergeambiguity";

    internal static bool Compatible(InventoryExtractedRow left, InventoryExtractedRow right, string sourceHash)
    {
        var first = InventoryCandidateNormalizer.Normalize(left, sourceHash, DateTimeOffset.UnixEpoch).Values;
        var second = InventoryCandidateNormalizer.Normalize(right, sourceHash, DateTimeOffset.UnixEpoch).Values;
        return Compatible(Material(first), Material(second));
    }

    private static JsonElement Material(InventoryCandidateValues value) => JsonSerializer.SerializeToElement(new
    {
        value.ProductCode, value.Currency, value.RateType, value.Geography, value.Address,
        value.Latitude, value.Longitude, value.CommercialTerms, value.Deliverable, value.Spatial, value.Package,
    });

    private static bool Compatible(JsonElement left, JsonElement right)
    {
        if (Missing(left) || Missing(right)) return true;
        if (left.ValueKind != right.ValueKind) return false;
        if (left.ValueKind == JsonValueKind.Object)
            return left.EnumerateObject().All(property =>
                !right.TryGetProperty(property.Name, out var value) || Compatible(property.Value, value));
        if (left.ValueKind == JsonValueKind.String)
            return string.Equals(left.GetString()?.Trim(), right.GetString()?.Trim(), StringComparison.OrdinalIgnoreCase);
        return left.GetRawText() == right.GetRawText();
    }

    private static bool Missing(JsonElement value) => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()) ||
        value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;

    internal static bool SamePhysicalEvidence(InventoryExtractedRow left, InventoryExtractedRow right) =>
        left.Locator == right.Locator || left.FieldLocators is not null && right.FieldLocators is not null &&
        left.FieldLocators.Any(field => (field.Key is "name" or "rate" or "price" or "productcode") &&
            right.FieldLocators.TryGetValue(field.Key, out var locator) &&
            locator == field.Value && locator != left.Locator && locator != right.Locator);

    internal static InventoryExtractedRow MarkAmbiguous(InventoryExtractedRow row)
    {
        var values = row.Values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        values[AmbiguityField] = "Provider row has multiple compatible physical source rows; exact binding requires review.";
        return row with { Values = values };
    }
}
