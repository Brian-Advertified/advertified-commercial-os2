using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class NativeOfficeTableProjection
{
    [GeneratedRegex(
        @"\b(?:20\d{2}[/.-]\d{1,2}[/.-]\d{1,2}|\d{1,2}[/.-]\d{1,2}[/.-]20\d{2})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    internal static InventoryExtractedRow[] Project(
        InventoryTableRow[] rows,
        int rowOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        IReadOnlyDictionary<string, string>? context = null,
        string? contextLocator = null)
    {
        if (rows.Length < 2) return [];
        var keyValue = InventoryKeyValueTableProjection.Project(
            rows,
            rowOffset,
            rowLocator,
            cellLocator,
            context,
            contextLocator);
        if (keyValue.Length > 0)
            return keyValue;
        var headerRow = SelectHeaderRow(rows);
        if (headerRow is null)
        {
            return InventoryHeaderlessRateTableProjection.Project(
                rows,
                rowOffset,
                rowLocator,
                cellLocator,
                context,
                contextLocator);
        }
        var headers = Headers(rows, headerRow.Value);
        var data = rows.Where(row => row.SourceRow > headerRow.Value).ToArray();
        var scheduled = ProjectSchedule(
            headers, data, rowOffset, cellLocator);
        var projected = scheduled.Length > 0
            ? scheduled
            : InventoryTabularProjection.Project(
                headers, data, rowOffset, rowLocator, cellLocator);
        return projected
            .Select(row => AddContext(
                row with
                {
                    ExtractionMethod =
                        MasterDataCodes.InventoryExtractionMethods.Tabular,
                },
                context,
                contextLocator))
            .ToArray();
    }

    private static int? SelectHeaderRow(
        IReadOnlyList<InventoryTableRow> rows)
    {
        var selection = rows.OrderBy(row => row.SourceRow)
            .Take(12)
            .Select(row => new
            {
                row.SourceRow,
                Score = row.Cells.Values.Count(value =>
                    InventoryCandidateNormalizer.RecognizesHeader(
                        InventoryTabularProjection.NormalizeHeader(value)) ||
                    DatePattern().IsMatch(value)),
                Count = row.Cells.Values.Count(value =>
                    !string.IsNullOrWhiteSpace(value)),
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Count)
            .ThenBy(item => item.SourceRow)
            .FirstOrDefault();
        return selection is not null && selection.Score > 0
            ? selection.SourceRow
            : null;
    }

    private static Dictionary<int, string> Headers(
        IReadOnlyList<InventoryTableRow> rows,
        int headerRow) =>
        rows.Where(row => row.SourceRow <= headerRow)
            .SelectMany(row => row.Cells.Select(cell => new
            {
                Row = row.SourceRow,
                Column = cell.Key,
                cell.Value,
            }))
            .GroupBy(cell => cell.Column)
            .Select(group => new
            {
                Column = group.Key,
                Value = group.OrderByDescending(cell => cell.Row)
                    .Select(cell => cell.Value)
                    .FirstOrDefault(value =>
                        !string.IsNullOrWhiteSpace(value))
                    ?? string.Empty,
            })
            .Where(item => item.Value.Length > 0)
            .ToDictionary(item => item.Column, item => item.Value);

    private static InventoryExtractedRow[] ProjectSchedule(
        IReadOnlyDictionary<int, string> headers,
        IReadOnlyList<InventoryTableRow> rows,
        int rowOffset,
        Func<int, int, string> locator)
    {
        var dates = headers
            .Where(item => DatePattern().IsMatch(item.Value))
            .ToDictionary(item => item.Key, item => item.Value);
        if (dates.Count < 2) return [];
        var result = new List<InventoryExtractedRow>();
        foreach (var row in rows)
            AddScheduleRow(result, row, dates, rowOffset, locator);
        return result.ToArray();
    }

    private static void AddScheduleRow(
        List<InventoryExtractedRow> result,
        InventoryTableRow row,
        Dictionary<int, string> dates,
        int rowOffset,
        Func<int, int, string> locator)
    {
        var label = row.Cells.OrderBy(item => item.Key)
            .FirstOrDefault(item => !dates.ContainsKey(item.Key));
        foreach (var date in dates)
        {
            if (!row.Cells.TryGetValue(date.Key, out var raw) ||
                !TryOffer(raw, out var name, out var rate, out var currency))
                continue;
            var evidence = locator(row.SourceRow, date.Key);
            var values = new SortedDictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["currency"] = currency,
                ["name"] = name,
                ["rate"] = rate,
                ["ratetype"] = MasterDataCodes.RateTypes.SpotRate,
                ["scheduledate"] = date.Value,
            };
            if (!string.IsNullOrWhiteSpace(label.Value))
                values["timeslot"] = label.Value.Trim();
            result.Add(CreateScheduleRow(
                rowOffset + result.Count + 1,
                values, evidence, locator(row.SourceRow, label.Key)));
        }
    }

    private static InventoryExtractedRow CreateScheduleRow(
        int number,
        IReadOnlyDictionary<string, string> values,
        string evidence,
        string timeLocator)
    {
        var locators = values.Keys.ToDictionary(
            key => key,
            key => key == "timeslot" ? timeLocator : evidence,
            StringComparer.Ordinal);
        var bases = new Dictionary<string, string>
        {
            ["ratetype"] =
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
        };
        var transformations = new Dictionary<string, string>
        {
            ["ratetype"] = MasterDataCodes.InventoryTransformationTypes
                .DerivedFromSourceContext,
        };
        return new InventoryExtractedRow(
            number, evidence, values,
            MasterDataCodes.InventoryExtractionMethods.Tabular,
            null, locators, null, bases, transformations);
    }

    private static bool TryOffer(
        string raw,
        out string name,
        out string rate,
        out string currency)
    {
        name = string.Empty;
        rate = string.Empty;
        currency = string.Empty;
        var lines = raw.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        for (var index = lines.Length - 1; index >= 0; index--)
        {
            if (!InventoryMoneyParser.TryParse(
                    lines[index], out _, out currency) ||
                currency.Length == 0)
                continue;
            rate = lines[index];
            name = string.Join(' ', lines.Take(index)).Trim();
            return name.Length > 0;
        }
        return false;
    }

    private static InventoryExtractedRow AddContext(
        InventoryExtractedRow row,
        IReadOnlyDictionary<string, string>? context,
        string? locator)
    {
        if (context is null || context.Count == 0)
            return row;
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(
                item => item.Key, item => item.Value,
                StringComparer.Ordinal),
            StringComparer.Ordinal);
        var locators = row.FieldLocators?.ToDictionary(
            item => item.Key, item => item.Value,
            StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in context)
        {
            if (values.TryAdd(item.Key, item.Value))
                locators[item.Key] = locator ?? row.Locator;
        }
        return row with
        {
            Values = values,
            FieldLocators = locators,
        };
    }
}
