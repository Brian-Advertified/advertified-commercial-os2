using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryKeyValueTableProjection
{
    private static readonly Dictionary<string, string> DirectFields =
        new(StringComparer.Ordinal)
        {
            ["description"] = "description",
            ["name"] = "name",
            ["sitename"] = "name",
            ["site"] = "name",
            ["siteid"] = "productcode",
            ["sitecode"] = "productcode",
            ["code"] = "productcode",
            ["reference"] = "productcode",
            ["format"] = "format",
            ["size"] = "dimensions",
            ["dimensions"] = "dimensions",
            ["type"] = "creativespecification",
            ["driversside"] = "trafficdirection",
            ["trafficdirection"] = "trafficdirection",
            ["targetmall"] = "venue",
            ["venue"] = "venue",
            ["mall"] = "venue",
            ["notes"] = "conditions",
            ["conditions"] = "conditions",
            ["availability"] = "availability",
            ["trafficcount"] = "audiencefootfall",
            ["impacts"] = "audienceimpressions",
            [MasterDataCodes.InventoryUnsupportedClaimTerms.Impressions] =
                "audienceimpressions",
            ["frequency"] = "frequency",
        };

    private static readonly HashSet<string> GeographyLabels =
        new(StringComparer.Ordinal)
        {
            "area", "cityprov", "cityprovince",
        };

    private static readonly Dictionary<string, int> RatePriorities =
        new(StringComparer.Ordinal)
        {
            ["rate"] = 1,
            ["grossrate"] = 1,
            ["ratecard"] = 2,
            ["discountedrate"] = 3,
            ["netrate"] = 4,
        };

    private static readonly HashSet<string> CoordinateLabels =
        new(StringComparer.Ordinal)
        {
            "gps", "coordinates",
        };

    [GeneratedRegex(
        @"(?<width>\d+(?:[.,]\d+)?)\s*m?\s*[x×]\s*(?<height>\d+(?:[.,]\d+)?)\s*m?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DimensionInTextPattern();

    [GeneratedRegex(
        @"(?<latd>\d{1,2})\s*°\s*(?<latm>\d{1,2})\s*['’]\s*(?<lats>\d+(?:[.,]\d+)?)\s*[""”]?\s*(?<latc>[NS]).*?(?<lond>\d{1,3})\s*°\s*(?<lonm>\d{1,2})\s*['’]\s*(?<lons>\d+(?:[.,]\d+)?)\s*[""”]?\s*(?<lonc>[EW])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
        RegexOptions.Singleline)]
    private static partial Regex CoordinatePairPattern();

    internal static InventoryExtractedRow[] Project(
        InventoryTableRow[] rows,
        int rowNumberOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        IReadOnlyDictionary<string, string>? context = null,
        string? contextLocator = null)
    {
        var pairs = ReadPairs(rows);
        if (!LooksLikeKeyValueTable(pairs))
            return [];

        var state = new ProjectionState();
        foreach (var pair in pairs)
            ApplyPair(state, pair, cellLocator);

        CompleteProjection(
            state,
            context,
            contextLocator ?? rowLocator(rows[0].SourceRow));
        if (!state.Values.ContainsKey("name") &&
            !state.Values.ContainsKey("productcode"))
        {
            return [];
        }

        return [new InventoryExtractedRow(
            rowNumberOffset + 1,
            rowLocator(rows[0].SourceRow),
            state.Values,
            MasterDataCodes.InventoryExtractionMethods.Tabular,
            null,
            state.Locators,
            null,
            state.Bases,
            state.Transformations)];
    }

    private static Pair[] ReadPairs(IEnumerable<InventoryTableRow> rows) =>
        rows.Select(ReadPair)
            .Where(pair => pair is not null)
            .Select(pair => pair!)
            .ToArray();

    private static Pair? ReadPair(InventoryTableRow row)
    {
        var cells = row.Cells
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .OrderBy(item => item.Key)
            .ToArray();
        if (cells.Length != 2)
            return null;
        var label = InventoryTabularProjection.NormalizeHeader(
            cells[0].Value);
        var value = cells[1].Value.Trim();
        return label.Length == 0 || value.Length == 0
            ? null
            : new Pair(row.SourceRow, label, value, cells[1].Key);
    }

    private static bool LooksLikeKeyValueTable(
        IReadOnlyCollection<Pair> pairs) =>
        pairs.Count >= 3 &&
        pairs.Count(pair =>
            DirectFields.ContainsKey(pair.Label) ||
            GeographyLabels.Contains(pair.Label) ||
            RatePriorities.ContainsKey(pair.Label) ||
            CoordinateLabels.Contains(pair.Label)) >= 3;

    private static void ApplyPair(
        ProjectionState state,
        Pair pair,
        Func<int, int, string> cellLocator)
    {
        var locator = cellLocator(pair.Row, pair.ValueColumn);
        if (GeographyLabels.Contains(pair.Label))
        {
            AddUnique(state.Geographies, pair.Value);
            state.GeographyLocator ??= locator;
            return;
        }
        if (RatePriorities.TryGetValue(
                pair.Label, out var priority))
        {
            state.Rates.Add(new RateOption(
                priority, pair.Value, locator));
            return;
        }
        if (CoordinateLabels.Contains(pair.Label))
        {
            AddCoordinates(state, pair.Value, locator);
            return;
        }
        if (DirectFields.TryGetValue(
                pair.Label, out var field))
        {
            if (field == "venue" && string.Equals(
                    pair.Value, "n/a",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Set(state, field, pair.Value, locator);
        }
    }

    private static void CompleteProjection(
        ProjectionState state,
        IReadOnlyDictionary<string, string>? context,
        string contextLocator)
    {
        if (state.Geographies.Count > 0)
        {
            Set(
                state,
                "geography",
                string.Join(" | ", state.Geographies),
                state.GeographyLocator ?? contextLocator);
        }
        AddBestRate(state);
        AddDimensionsFromFormat(state);
        AddContext(state, context, contextLocator);
        AddDerivedChannel(state);
        AddFallbackName(state, contextLocator);
    }

    private static void AddBestRate(ProjectionState state)
    {
        var valid = state.Rates
            .Where(option =>
                InventoryMoneyParser.TryParse(
                    option.Value, out _, out var currency) &&
                currency.Length > 0)
            .OrderByDescending(option => option.Priority)
            .FirstOrDefault();
        if (valid is not null)
            Set(state, "rate", valid.Value, valid.Locator);
    }

    private static void AddDimensionsFromFormat(
        ProjectionState state)
    {
        if (state.Values.ContainsKey("dimensions") ||
            !state.Values.TryGetValue("format", out var format))
        {
            return;
        }
        var match = DimensionInTextPattern().Match(format);
        if (!match.Success)
            return;
        state.Values["dimensions"] =
            match.Groups["width"].Value.Replace(',', '.') + " x " +
            match.Groups["height"].Value.Replace(',', '.');
        state.Locators["dimensions"] = state.Locators["format"];
        state.Transformations["dimensions"] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
    }

    private static void AddCoordinates(
        ProjectionState state,
        string raw,
        string locator)
    {
        var match = CoordinatePairPattern().Match(raw);
        if (!match.Success)
            return;
        Set(
            state, "latitude",
            Coordinate(match, "lat", 'S'), locator);
        Set(
            state, "longitude",
            Coordinate(match, "lon", 'W'), locator);
        state.Transformations["latitude"] = MasterDataCodes
            .InventoryTransformationTypes.ParseDecimal;
        state.Transformations["longitude"] = MasterDataCodes
            .InventoryTransformationTypes.ParseDecimal;
    }

    private static string Coordinate(
        Match match,
        string prefix,
        char negativeDirection)
    {
        var degrees = decimal.Parse(
            match.Groups[prefix + "d"].Value,
            CultureInfo.InvariantCulture);
        var minutes = decimal.Parse(
            match.Groups[prefix + "m"].Value,
            CultureInfo.InvariantCulture);
        var seconds = decimal.Parse(
            match.Groups[prefix + "s"].Value.Replace(',', '.'),
            CultureInfo.InvariantCulture);
        var value = degrees + minutes / 60m + seconds / 3600m;
        if (char.ToUpperInvariant(
                match.Groups[prefix + "c"].Value[0]) ==
            negativeDirection)
        {
            value = -value;
        }
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static void AddContext(
        ProjectionState state,
        IReadOnlyDictionary<string, string>? context,
        string locator)
    {
        if (context is null)
            return;
        foreach (var item in context.Where(item =>
                     !item.Key.StartsWith("__", StringComparison.Ordinal)))
        {
            Set(state, item.Key, item.Value, locator);
        }
    }

    private static void AddDerivedChannel(ProjectionState state)
    {
        if (state.Values.ContainsKey("channel"))
            return;
        var source = string.Join(' ', new[]
        {
            Value(state.Values, "name"),
            Value(state.Values, "format"),
            Value(state.Values, "creativespecification"),
            Value(state.Values, "description"),
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var channel = Regex.IsMatch(
                source,
                @"\bdigital\b|\bDOOH\b|screen",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? MasterDataCodes.Channels.Dooh
            : Regex.IsMatch(
                source,
                @"billboard|outdoor",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                ? MasterDataCodes.Channels.Ooh
                : null;
        if (channel is null)
            return;
        var locator = Value(state.Locators, "format") ??
            Value(state.Locators, "description") ??
            state.Locators.Values.First();
        Set(state, "channel", channel, locator);
        state.Bases["channel"] = MasterDataCodes
            .InventoryEvidenceBases.DerivedPolicy;
        state.Transformations["channel"] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
    }

    private static void AddFallbackName(
        ProjectionState state,
        string locator)
    {
        if (state.Values.ContainsKey("name") ||
            state.Values.ContainsKey("productcode"))
        {
            return;
        }
        var name = string.Join(" - ", new[]
        {
            Value(state.Values, "geography"),
            Value(state.Values, "format"),
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (name.Length == 0 &&
            state.Values.TryGetValue(
                "description", out var description))
        {
            name = description.Length <= 160
                ? description
                : description[..160];
        }
        if (name.Length > 0)
            Set(state, "name", name, locator);
    }

    private static void Set(
        ProjectionState state,
        string key,
        string value,
        string locator)
    {
        if (value.Length == 0 ||
            !state.Values.TryAdd(key, value))
        {
            return;
        }
        state.Locators[key] = locator;
    }

    private static string? Value(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        values.TryGetValue(key, out var value)
            ? value
            : null;

    private static void AddUnique(
        List<string> values,
        string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            values.Add(value);
    }
}
