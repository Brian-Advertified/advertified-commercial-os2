using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    [GeneratedRegex(
        @"^\s*\d{1,2}:\d{2}\s*[-–—]\s*\d{1,2}:\d{2}\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RadioTimeBandPattern();

    private static InventoryExtractedRow[] ReadRadioSchedule(
        InventoryTableRow[] rows,
        IReadOnlyList<TextItem> texts,
        int tableNumber,
        int page,
        int rowOffset)
    {
        if (rows.Length < 2)
            return [];
        var header = rows.OrderBy(row => row.SourceRow).First();
        var columns = header.Cells.Keys.Order().ToArray();
        if (columns.Length < 4 || columns.Length % 2 != 0)
            return [];
        for (var index = 0; index < columns.Length; index += 2)
        {
            var time = InventoryTabularProjection.NormalizeHeader(
                header.Cells.GetValueOrDefault(columns[index]) ?? string.Empty);
            var rate = InventoryTabularProjection.NormalizeHeader(
                header.Cells.GetValueOrDefault(columns[index + 1]) ?? string.Empty);
            if (time != "timeband" ||
                rate is not ("netrates" or "netrate" or "rates" or "rate"))
            {
                return [];
            }
        }

        var pageTexts = texts
            .Where(item => item.Page == page)
            .Select(item => item.Text)
            .ToArray();
        var station = ReadRadioStation(pageTexts);
        if (station is null)
            return [];
        var dayLabels = RadioDayLabels(pageTexts, columns.Length / 2);
        var description = pageTexts.FirstOrDefault(text =>
            text.StartsWith(
                station + " ", StringComparison.OrdinalIgnoreCase));
        var result = new List<InventoryExtractedRow>();
        foreach (var row in rows.Where(row =>
                     row.SourceRow > header.SourceRow))
        {
            for (var pair = 0; pair < columns.Length / 2; pair++)
            {
                var timeColumn = columns[pair * 2];
                var rateColumn = columns[pair * 2 + 1];
                if (!row.Cells.TryGetValue(timeColumn, out var timeBand) ||
                    !row.Cells.TryGetValue(rateColumn, out var rawRate) ||
                    !RadioTimeBandPattern().IsMatch(timeBand) ||
                    !InventoryMoneyParser.TryParse(
                        rawRate, out _, out _))
                {
                    continue;
                }
                var day = dayLabels[pair];
                var locator = CellLocator(
                    page, tableNumber, row.SourceRow, rateColumn);
                var values = new SortedDictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["channel"] = MasterDataCodes.Channels.Radio,
                    ["currency"] = MasterDataCodes.Currencies.Zar,
                    ["daypart"] = timeBand.Trim(),
                    ["name"] = station + " - " + day + " - " +
                        timeBand.Trim(),
                    ["placement"] = "Radio spot",
                    ["rate"] = rawRate.Trim(),
                    ["ratetype"] = MasterDataCodes.RateTypes.SpotRate,
                };
                if (!string.IsNullOrWhiteSpace(description))
                    values["description"] = Limit(description, 2_000);
                var locators = values.Keys.ToDictionary(
                    key => key,
                    key => key switch
                    {
                        "daypart" => CellLocator(
                            page, tableNumber, row.SourceRow, timeColumn),
                        "description" => "docling:page=" + page +
                            ";radio-description=1",
                        _ => locator,
                    },
                    StringComparer.Ordinal);
                var bases = new Dictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["channel"] = MasterDataCodes
                        .InventoryEvidenceBases.DerivedPolicy,
                    ["currency"] = MasterDataCodes
                        .InventoryEvidenceBases.DerivedPolicy,
                    ["name"] = MasterDataCodes
                        .InventoryEvidenceBases.DerivedPolicy,
                    ["placement"] = MasterDataCodes
                        .InventoryEvidenceBases.DerivedPolicy,
                    ["ratetype"] = MasterDataCodes
                        .InventoryEvidenceBases.DerivedPolicy,
                };
                var transformations = bases.Keys.ToDictionary(
                    key => key,
                    _ => MasterDataCodes.InventoryTransformationTypes
                        .DerivedFromSourceContext,
                    StringComparer.Ordinal);
                result.Add(new InventoryExtractedRow(
                    rowOffset + result.Count + 1,
                    "docling:page=" + page +
                        ";table=" + tableNumber +
                        ";radio-row=" + row.SourceRow +
                        ";day=" + (pair + 1).ToString(
                            CultureInfo.InvariantCulture),
                    values,
                    MasterDataCodes.InventoryExtractionMethods.Tabular,
                    null,
                    locators,
                    null,
                    bases,
                    transformations));
            }
        }
        return result.ToArray();
    }

    private static string? ReadRadioStation(
        IEnumerable<string> pageTexts)
    {
        foreach (var text in pageTexts)
        {
            var line = text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries).FirstOrDefault();
            if (line is null)
                continue;
            var separator = line.IndexOf(
                " is ", StringComparison.OrdinalIgnoreCase);
            var candidate = separator > 0
                ? line[..separator].Trim()
                : line.Trim();
            if (candidate.Length is < 2 or > 60)
                continue;
            if (Regex.IsMatch(
                    candidate,
                    @"\bFM\b|\bRADIO\b|^RSG$|^SAFM$|CHANNEL\s+AFRICA",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
            {
                return NormalizeStationName(candidate);
            }
        }
        return null;
    }

    private static string NormalizeStationName(string value)
    {
        if (string.Equals(
                value, "SAFM", StringComparison.OrdinalIgnoreCase))
            return "SAfm";
        return string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries));
    }

    private static string[] RadioDayLabels(
        IReadOnlyList<string> pageTexts,
        int count)
    {
        var source = pageTexts.FirstOrDefault(text =>
            text.Contains(
                "MONDAY", StringComparison.OrdinalIgnoreCase) &&
            text.Contains(
                "SATURDAY", StringComparison.OrdinalIgnoreCase));
        var labels = source?.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(value => Regex.Replace(
                value.ToUpperInvariant(),
                @"[^A-Z]+",
                "_",
                RegexOptions.CultureInvariant).Trim('_'))
            .Where(value => value.Length > 0)
            .Take(count)
            .ToArray() ?? [];
        var defaults = new[]
        {
            "MONDAY_FRIDAY", "SATURDAY", "SUNDAY",
            "DAY_4", "DAY_5", "DAY_6",
        };
        return Enumerable.Range(0, count)
            .Select(index => index < labels.Length
                ? labels[index]
                : defaults[index])
            .ToArray();
    }
}
