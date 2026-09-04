using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryHeaderlessRateTableProjection
{
    internal static InventoryExtractedRow[] Project(
        InventoryTableRow[] rows,
        int rowNumberOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        IReadOnlyDictionary<string, string>? context = null,
        string? contextLocator = null)
    {
        var result = new List<InventoryExtractedRow>();
        foreach (var row in rows.OrderBy(item => item.SourceRow))
        {
            AddRows(
                result,
                row,
                rowNumberOffset,
                rowLocator,
                cellLocator,
                context,
                contextLocator);
        }
        return result.ToArray();
    }

    private static void AddRows(
        List<InventoryExtractedRow> result,
        InventoryTableRow row,
        int rowNumberOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        IReadOnlyDictionary<string, string>? context,
        string? contextLocator)
    {
        var rateCells = row.Cells
            .Select(item => new
            {
                item.Key,
                Lines = Lines(item.Value)
                    .Where(IsCurrencyRate)
                    .ToArray(),
            })
            .Where(item => item.Lines.Length > 0)
            .ToArray();
        if (rateCells.Length != 1)
            return;

        var rateCell = rateCells[0];
        var textCells = row.Cells
            .Where(item => item.Key != rateCell.Key)
            .OrderBy(item => item.Key)
            .Select(item => new TextCell(
                item.Key,
                Lines(item.Value)))
            .Where(item => item.Lines.Length > 0)
            .ToArray();
        if (textCells.Length == 0)
            return;

        for (var index = 0;
             index < rateCell.Lines.Length; index++)
        {
            var name = CandidateName(
                textCells, index, rateCell.Lines.Length);
            if (name is null)
                continue;
            var description = Description(textCells, index);
            var rate = rateCell.Lines[index];
            var locator = cellLocator(
                row.SourceRow, rateCell.Key);
            result.Add(CreateRow(
                rowNumberOffset + result.Count + 1,
                rowLocator(row.SourceRow),
                name,
                description,
                rate,
                locator,
                textCells[0].Key,
                row.SourceRow,
                cellLocator,
                context,
                contextLocator));
        }
    }

    private static InventoryExtractedRow CreateRow(
        int number,
        string locator,
        string name,
        string? description,
        string rate,
        string rateLocator,
        int nameColumn,
        int sourceRow,
        Func<int, int, string> cellLocator,
        IReadOnlyDictionary<string, string>? context,
        string? contextLocator)
    {
        var values = new SortedDictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["currency"] = MasterDataCodes.Currencies.Zar,
            ["name"] = name,
            ["rate"] = rate,
        };
        var locators = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["currency"] = rateLocator,
            ["name"] = cellLocator(sourceRow, nameColumn),
            ["rate"] = rateLocator,
        };
        if (!string.IsNullOrWhiteSpace(description))
        {
            values["description"] = description;
            locators["description"] = locator;
            values["placement"] = description;
            locators["placement"] = locator;
        }
        AddContext(
            values, locators, context, contextLocator ?? locator);
        AddDerivedCommercialMeaning(
            values, locators, rate, description, name, rateLocator);
        return new InventoryExtractedRow(
            number,
            locator,
            values,
            MasterDataCodes.InventoryExtractionMethods.Tabular,
            null,
            locators,
            null,
            DerivedBases(values),
            DerivedTransformations(values));
    }

    private static string? CandidateName(
        IReadOnlyList<TextCell> textCells,
        int index,
        int rateCount)
    {
        foreach (var cell in textCells)
        {
            var lines = cell.Lines;
            if (lines.Length == rateCount && index < lines.Length)
            {
                var candidate = CleanName(lines[index]);
                if (candidate is not null)
                    return candidate;
            }
            if (index == 0)
            {
                var candidate = CleanName(lines[0]);
                if (candidate is not null)
                    return candidate;
            }
        }
        return null;
    }

    private static string? Description(
        IReadOnlyList<TextCell> textCells,
        int index)
    {
        var values = new List<string>();
        foreach (var cell in textCells)
        {
            var lines = cell.Lines;
            var value = index < lines.Length
                ? lines[index]
                : string.Join(" | ", lines);
            if (!string.IsNullOrWhiteSpace(value) &&
                !values.Contains(
                    value, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(value.Trim());
            }
        }
        return values.Count <= 1
            ? null
            : string.Join(" | ", values.Skip(1));
    }

    private static string[] Lines(string value) =>
        value.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

    private static bool IsCurrencyRate(string value) =>
        InventoryMoneyParser.TryParse(
            value, out _, out var currency) &&
        currency.Length > 0;

    private static string? CleanName(string value)
    {
        var candidate = value.Trim(' ', '-', ':', '|');
        if (candidate.Length is < 2 or > 500 ||
            candidate.All(character =>
                !char.IsLetterOrDigit(character)) ||
            InventoryMoneyParser.TryParse(
                candidate, out _, out var currency) &&
            currency.Length > 0)
        {
            return null;
        }
        return candidate;
    }

    private static void AddContext(
        SortedDictionary<string, string> values,
        Dictionary<string, string> locators,
        IReadOnlyDictionary<string, string>? context,
        string locator)
    {
        if (context is null)
            return;
        foreach (var item in context.Where(item =>
                     !item.Key.StartsWith("__", StringComparison.Ordinal)))
        {
            if (values.TryAdd(item.Key, item.Value))
                locators[item.Key] = locator;
        }
    }

    private static void AddDerivedCommercialMeaning(
        SortedDictionary<string, string> values,
        Dictionary<string, string> locators,
        string rate,
        string? description,
        string name,
        string locator)
    {
        var source = string.Join(' ', name, description, rate);
        var channel = Channel(source);
        if (channel is not null &&
            values.TryAdd("channel", channel))
        {
            locators["channel"] = locator;
        }
        var rateType = RateType(source);
        if (rateType is not null &&
            values.TryAdd("ratetype", rateType))
        {
            locators["ratetype"] = locator;
        }
    }

    private static string? Channel(string source)
    {
        if (Regex.IsMatch(
                source,
                @"facebook|instagram|tiktok|social|\bpost\b|\breel\b|stories",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Social;
        }
        if (Regex.IsMatch(
                source,
                @"digital|display|video|banner|website|home\s*page|entire\s+site|google\s+ads|programmatic|pre[- ]?roll",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Digital;
        }
        if (Regex.IsMatch(
                source,
                @"full\s+page|half\s+page|quarter\s+page|front\s+page|print",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Print;
        }
        return null;
    }

    private static string? RateType(string source)
    {
        if (Regex.IsMatch(
                source,
                @"\bCPM\b|per\s+1[,. ]?000",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.RateTypes.Cpm;
        }
        if (Regex.IsMatch(
                source,
                @"\bper\s+day\b|\bdaily\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.RateTypes.DayRate;
        }
        if (Regex.IsMatch(
                source,
                @"\bper\s+week\b|\bweekly\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.RateTypes.WeekRate;
        }
        if (Regex.IsMatch(
                source,
                @"\bper\s+month\b|\bmonthly\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.RateTypes.MonthRate;
        }
        if (Regex.IsMatch(
                source,
                @"\bpackage\b|\bbundle\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.RateTypes.PackageRate;
        }
        return null;
    }

    private static Dictionary<string, string> DerivedBases(
        SortedDictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["currency"] = MasterDataCodes
                .InventoryEvidenceBases.DerivedPolicy,
        };
        foreach (var field in new[] { "channel", "ratetype" })
        {
            if (values.ContainsKey(field))
                result[field] = MasterDataCodes
                    .InventoryEvidenceBases.DerivedPolicy;
        }
        return result;
    }

    private static Dictionary<string, string> DerivedTransformations(
        SortedDictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["currency"] = MasterDataCodes
                .InventoryTransformationTypes.ParseCurrencyAmount,
        };
        foreach (var field in new[] { "channel", "ratetype" })
        {
            if (values.ContainsKey(field))
                result[field] = MasterDataCodes
                    .InventoryTransformationTypes.DerivedFromSourceContext;
        }
        return result;
    }

    private sealed record TextCell(
        int Key,
        string[] Lines);
}
