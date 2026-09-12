using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryDocumentStructureBuilder
{
    internal const string StructuredCellKind = "cell";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    internal static bool CanDiscover(InventoryExtractionResult extraction) =>
        extraction.Document.SourceElements is { Count: > 0 } elements &&
        elements.All(item => item.StructureKind == StructuredCellKind);

    internal static InventoryDocumentStructure Build(InventoryExtractionResult extraction)
    {
        var elements = extraction.Document.SourceElements ?? [];
        var structures = elements
            .GroupBy(item => item.StructureId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new InventorySourceStructure(
                group.Key,
                group.Select(item => item.StructureKind).Distinct(StringComparer.Ordinal).Single(),
                group.OrderBy(item => item.Row).ThenBy(item => item.Column)
                    .ThenBy(item => item.Locator, StringComparer.Ordinal)
                    .Select(item => new InventorySourceCell(
                        item.Locator, item.Row, item.Column, item.RawValue, item.PositionJson))
                    .ToArray()))
            .ToArray();
        if (structures.Length == 0)
            throw new InventorySchemaRejectedException("The source has no structured text for schema discovery.");
        var structuralPayload = JsonSerializer.Serialize(
            structures.Select(item => new
            {
                item.Id,
                item.Kind,
                cells = item.Cells.Select(cell => new
                {
                    cell.Locator,
                    cell.Row,
                    cell.Column,
                    cell.RawText,
                    cell.PositionJson,
                }),
            }), Json);
        var structureHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(structuralPayload)));
        return new InventoryDocumentStructure(
            extraction.SourceHash, structureHash, structures);
    }

    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> GovernedCodes(
        InventoryCodeSets codes) => new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["channel"] = codes.Channels,
        ["product_type"] = codes.ProductTypes,
        ["rate_type"] = codes.RateTypes,
        ["currency"] = codes.Currencies,
        ["availability"] = codes.Availability,
        ["performance_metric"] = codes.PerformanceMetrics,
        ["measurement_unit"] = codes.MeasurementUnits,
        ["vat_status"] = codes.VatStatuses,
        ["vat_treatment"] = codes.VatTreatments,
    };
}
