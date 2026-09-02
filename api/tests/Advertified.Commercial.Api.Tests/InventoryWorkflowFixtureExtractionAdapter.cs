using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Api.Tests;

internal sealed class InventoryWorkflowFixtureExtractionAdapter :
    IInventoryDocumentExtractionAdapter
{
    public Task<InventoryExtractionResult> ExtractAsync(
        InventoryExtractionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = request.FileName switch
        {
            "held-out-radio.xlsx" => [RadioRow("RAD-XLSX", "Metro FM", "25000")],
            "held-out-radio.docx" => [RadioRow("RAD-DOCX", "702", "20000")],
            "held-out-radio.pptx" => [RadioRow("RAD-PPTX", "Power FM", "23000")],
            "held-out-radio.pdf" => [RadioRow("RAD-PDF", "Cape Talk", "22000")],
            "held-out-site.png" or "held-out-site.jpg" => [EmptyRow(request.FileName)],
            _ => CsvRows(request),
        };
        var providerJson = JsonSerializer.Serialize(new { fixture = request.FileName, rows });
        return Task.FromResult(InventoryExtractionContract.Create(
            "advertified-test-fixture",
            "1.0.0",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            providerJson,
            rows));
    }

    private static InventoryExtractedRow RadioRow(
        string code,
        string name,
        string rate) => new(1, $"fixture:{code}", new Dictionary<string, string>
        {
            ["productcode"] = code,
            ["name"] = name,
            ["channel"] = "RADIO",
            ["geography"] = "Gauteng",
            ["ratetype"] = "SPOT_RATE",
            ["currency"] = "ZAR",
            ["rateminor"] = rate,
        });

    private static InventoryExtractedRow EmptyRow(string fileName) =>
        new(1, $"fixture:{fileName}", new Dictionary<string, string>());

    private static InventoryExtractedRow[] CsvRows(InventoryExtractionRequest request)
    {
        var lines = Encoding.UTF8.GetString(request.Content)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
        {
            return [EmptyRow(request.FileName)];
        }
        var headers = lines[0].Split(',').Select(NormalizeHeader).ToArray();
        return lines.Skip(1).Select((line, index) => new InventoryExtractedRow(
            index + 1,
            $"fixture:{request.FileName}#row={index + 2}",
            line.Split(',').Select((value, column) => (value, column))
                .Where(item => item.column < headers.Length &&
                    headers[item.column].Length > 0 && item.value.Trim().Length > 0)
                .ToDictionary(
                    item => headers[item.column], item => item.value.Trim(),
                    StringComparer.Ordinal))).ToArray();
    }

    private static string NormalizeHeader(string value) => new(
        value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
