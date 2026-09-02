using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Advertified.Commercial.Application.Inventory;

public static class InventoryExtractionContract
{
    private static readonly JsonSerializerOptions CanonicalJson = new(JsonSerializerDefaults.Web);

    public static InventoryExtractionResult Create(
        string adapterCode,
        string adapterVersion,
        string schemaVersion,
        string sourceHash,
        string providerJson,
        IReadOnlyList<InventoryExtractedRow> rows)
    {
        EnsureJson(providerJson);
        var document = new InventoryExtractionDocument(schemaVersion, CanonicalRows(rows));
        var canonicalJson = Serialize(document);
        return new InventoryExtractionResult(
            adapterCode,
            adapterVersion,
            sourceHash,
            providerJson,
            Hash(providerJson),
            canonicalJson,
            Hash(canonicalJson),
            document);
    }

    public static InventoryExtractionDocument Replay(
        string canonicalJson,
        string expectedSchemaVersion)
    {
        var document = JsonSerializer.Deserialize<InventoryExtractionDocument>(
            canonicalJson, CanonicalJson)
            ?? throw new InventoryExtractionUnavailableException();
        if (document.SchemaVersion != expectedSchemaVersion)
        {
            throw new InventoryExtractionUnavailableException();
        }
        return document with { Rows = CanonicalRows(document.Rows) };
    }

    public static string Serialize(InventoryExtractionDocument document) =>
        JsonSerializer.Serialize(document, CanonicalJson);

    public static string Hash(string json) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    private static InventoryExtractedRow[] CanonicalRows(
        IReadOnlyList<InventoryExtractedRow> rows)
    {
        if (rows.Any(row => row.Number <= 0 || string.IsNullOrWhiteSpace(row.Locator)) ||
            rows.Select(row => row.Number).Distinct().Count() != rows.Count)
        {
            throw new InventoryExtractionUnavailableException();
        }
        return rows.OrderBy(row => row.Number)
            .Select(row => row with
            {
                Locator = row.Locator.Trim(),
                Values = new SortedDictionary<string, string>(
                    row.Values.Where(item => !string.IsNullOrWhiteSpace(item.Key))
                        .ToDictionary(item => item.Key.Trim(), item => item.Value.Trim(),
                            StringComparer.Ordinal),
                    StringComparer.Ordinal),
                FieldLocators = CanonicalDictionary(row.FieldLocators),
                FieldConfidences = CanonicalDictionary(row.FieldConfidences),
            }).ToArray();
    }

    private static SortedDictionary<string, T>? CanonicalDictionary<T>(
        IReadOnlyDictionary<string, T>? values) => values is null
        ? null
        : new SortedDictionary<string, T>(
            values.Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Key.Trim(), item => item.Value,
                    StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static void EnsureJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            throw new InventoryExtractionUnavailableException();
        }
    }
}
