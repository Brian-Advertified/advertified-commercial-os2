using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Inventory;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class DoclingInventoryExtractionAdapterTests
{
    [Fact]
    public void SabcRadioSourceMapProjectsAllPhysicalTimeBandRates()
    {
        var sourcePath = Path.Combine(
            RepositoryRoot(),
            "artifacts",
            "inventory-corpus",
            "semantic-v1",
            "beff939cae1ab50a198de0812b9b82de0c50a063cfc7b37db365cf33691e8cc6.json");
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        var texts = source["fragments"]!.AsArray()
            .Where(item => !string.IsNullOrWhiteSpace(
                item?["text"]?.GetValue<string>()))
            .Select(item => new
            {
                text = item!["text"]!.GetValue<string>(),
                prov = new[]
                {
                    new
                    {
                        page_no = item["ordinal"]!.GetValue<int>(),
                    },
                },
            })
            .Cast<object>()
            .ToArray();
        var tables = source["tables"]!.AsArray()
            .Select(table => new
            {
                prov = new[]
                {
                    new
                    {
                        page_no = table!["page"]!.GetValue<int>(),
                    },
                },
                data = new
                {
                    table_cells = TableCells(table!["rows"]!.AsArray()),
                },
            })
            .Cast<object>()
            .ToArray();
        var json = JsonSerializer.Serialize(new { texts, tables });
        var request = new InventoryExtractionRequest(
            "SABC Radio Rates F2025-2026 (3) (1) (1) - Copy.pdf",
            "application/pdf",
            MasterDataCodes.DocumentClasses.Pdf,
            new string('f', 64),
            [1]);

        var rows = DoclingInventoryProjection.ReadRows(request, json);
        var provider = InventoryExtractionContract.Create(
            "docling",
            "test",
            InventoryExtractionOptions.CurrentSchemaVersion,
            request.SourceHash,
            json,
            rows);
        var contextual = InventorySourceContextProjection.Apply(
            request,
            provider);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            contextual.Rows,
            request.SourceHash,
            "Not supplied",
            EmptyCodes(),
            DateTimeOffset.UnixEpoch);

        var expectedByPage = ExpectedRadioRows(
            source["tables"]!.AsArray());
        var actualByPage = rows
            .Where(row =>
                row.Values.GetValueOrDefault("channel") ==
                MasterDataCodes.Channels.Radio &&
                row.Values.ContainsKey("daypart") &&
                row.Values.ContainsKey("rate"))
            .GroupBy(row => PageFromLocator(row.Locator))
            .ToDictionary(group => group.Key, group => group.Count());
        var admittedRadio = candidates.Count(candidate =>
            candidate.Values.Channel == MasterDataCodes.Channels.Radio &&
            candidate.Values.Deliverable?.Daypart is not null &&
            candidate.Values.RateAmountMinor is not null);
        var missingByPage = expectedByPage
            .Where(item => actualByPage.GetValueOrDefault(item.Key) != item.Value)
            .ToDictionary(
                item => item.Key,
                item => new
                {
                    Expected = item.Value,
                    Actual = actualByPage.GetValueOrDefault(item.Key),
                });
        var missingPageText = source["fragments"]!.AsArray()
            .Where(item => missingByPage.ContainsKey(
                item!["ordinal"]!.GetValue<int>()))
            .GroupBy(item => item!["ordinal"]!.GetValue<int>())
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item!["text"]?.ToString() ?? string.Empty)
                    .Where(value => value.Length > 0)
                    .Take(4)
                    .ToArray());
        var expectedTotal = expectedByPage.Values.Sum();
        var actualTotal = actualByPage.Values.Sum();
        Assert.True(
            actualTotal == expectedTotal,
            JsonSerializer.Serialize(new
            {
                expectedTotal,
                actualTotal,
                missingByPage,
                missingPageText,
            }));
        Assert.Equal(actualTotal, admittedRadio);
    }

    private static Dictionary<int, int> ExpectedRadioRows(JsonArray tables)
    {
        var result = new Dictionary<int, int>();
        foreach (var table in tables)
        {
            var rows = table!["rows"]!.AsArray();
            var headerIndex = Enumerable.Range(0, Math.Min(6, rows.Count))
                .FirstOrDefault(
                    index => RadioPairs(rows[index]!.AsArray()).Length > 0,
                    -1);
            if (headerIndex < 0)
                continue;
            var pairs = RadioPairs(rows[headerIndex]!.AsArray());
            var count = 0;
            foreach (var row in rows.Skip(headerIndex + 1))
            {
                var cells = row!.AsArray();
                count += pairs.Count(pair =>
                    RadioTime(CellValue(cells, pair.TimeColumn)) &&
                    NumericRate(CellValue(cells, pair.RateColumn)));
            }
            if (count > 0)
            {
                var page = table["page"]!.GetValue<int>();
                result[page] = result.GetValueOrDefault(page) + count;
            }
        }
        return result;
    }

    private static (int TimeColumn, int RateColumn)[] RadioPairs(
        JsonArray header)
    {
        var result = new List<(int, int)>();
        for (var column = 0; column + 1 < header.Count; column++)
        {
            var left = CellValue(header, column)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
            var right = CellValue(header, column + 1)
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
            if (left == "TIMEBAND" && right is "NETRATE" or "NETRATES")
            {
                result.Add((column, column + 1));
                column++;
            }
        }
        return result.ToArray();
    }

    private static string CellValue(JsonArray row, int column)
    {
        if (column >= row.Count)
            return string.Empty;
        var cell = row[column];
        return cell is JsonObject
            ? cell?["value"]?.ToString() ?? string.Empty
            : cell?.ToString() ?? string.Empty;
    }

    private static bool RadioTime(string value) =>
        Regex.IsMatch(
            value,
            @"^\s*\d{1,2}:\d{2}\s*[-–—]\s*\d{1,2}:\d{2}\s*$",
            RegexOptions.CultureInvariant);

    private static bool NumericRate(string value) =>
        Regex.IsMatch(
            value,
            @"^\s*\d[\d\s.,]*\s*$",
            RegexOptions.CultureInvariant);

    private static int PageFromLocator(string locator)
    {
        var match = Regex.Match(
            locator,
            @"page=(\d+)",
            RegexOptions.CultureInvariant);
        return match.Success
            ? int.Parse(
                match.Groups[1].Value,
                CultureInfo.InvariantCulture)
            : 0;
    }

    private static object[] TableCells(JsonArray rows)
    {
        var result = new List<object>();
        for (var row = 0; row < rows.Count; row++)
        {
            var cells = rows[row]!.AsArray();
            for (var column = 0; column < cells.Count; column++)
            {
                var cell = cells[column];
                var text = cell is JsonObject
                    ? cell?["value"]?.ToString()
                    : cell?.ToString();
                result.Add(Cell(text ?? string.Empty, row, column));
            }
        }
        return result.ToArray();
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ADVERTIFIED.md")))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("Advertified repository root not found.");
    }
}
