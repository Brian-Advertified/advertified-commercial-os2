using System.Globalization;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryHierarchicalMatrixProjection
{
    private static readonly string[] RateHeaderTerms =
    [
        "rate", "price", "cost", "cpm", "cpc", "cpv", "spot",
        "second", "duration", "weekday", "weekend", "daypart",
        "region", "geography", "market", "station", "channel",
        "publication",
    ];

    private static readonly string[] NonRateMeasureTerms =
    [
        MasterDataCodes.InventoryUnsupportedClaimTerms.Audience,
        MasterDataCodes.InventoryUnsupportedClaimTerms.Reach,
        MasterDataCodes.InventoryUnsupportedClaimTerms.Listeners[..^1],
        MasterDataCodes.InventoryUnsupportedClaimTerms.Impressions[..^1],
        MasterDataCodes.InventoryUnsupportedClaimTerms.Ratings[..^1],
        "quantity", "spots", "frequency", "index", "share",
    ];

    private static readonly HashSet<string> InheritableHeaders =
    [
        "productcode", "siteid", "stationcode", "name", "product",
        "productname", "sitename", "station", "stationname", "platform",
        "publication", "programme", "program", "show", "daypart",
        "timeslot", "geography", "location", "market", "area", "region",
        "province", "channel", "format", "type", "placement",
    ];

    [GeneratedRegex(@"(?<!\d)(?<seconds>\d{1,3})\s*(?:sec(?:ond)?s?|\x22)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}\s*(?:-|–|—|to)\s*\d{1,2}:\d{2}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TimeRangePattern();

    [GeneratedRegex(@"\b(?:20\d{2}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]20\d{2})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    internal static InventoryExtractedRow[] Project(
        IReadOnlyList<InventoryTableRow> rows,
        int headerRow,
        int rowNumberOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        Func<int, int, decimal?>? confidence = null,
        Func<int, int, string?>? position = null)
    {
        var dataRows = rows.Where(row => row.SourceRow > headerRow)
            .OrderBy(row => row.SourceRow).ToArray();
        if (dataRows.Length == 0) return [];
        var headers = ResolveHeaders(rows, headerRow, cellLocator);
        var rateColumns = headers.Values.Where(header =>
                IsRateColumn(header, dataRows, headers))
            .Select(header => header.Column).ToHashSet();
        if (rateColumns.Count < 2) return [];
        if (IsTransposedMatrix(headers, rateColumns))
            return ProjectTransposed(dataRows, headers, rateColumns,
                rowNumberOffset, cellLocator, confidence, position);
        var projectedRows = FillDown(dataRows, headers, rateColumns, cellLocator);
        var result = new List<InventoryExtractedRow>();
        foreach (var row in projectedRows)
            AddRow(result, row, headers, rateColumns, rowNumberOffset,
                rowLocator, cellLocator, confidence, position);
        return result.ToArray();
    }

    private static Dictionary<int, MatrixHeader> ResolveHeaders(
        IReadOnlyList<InventoryTableRow> rows,
        int headerRow,
        Func<int, int, string> locator)
    {
        var headerRows = rows.Where(row => row.SourceRow <= headerRow)
            .OrderBy(row => row.SourceRow).ToArray();
        var columns = rows.SelectMany(row => row.Cells.Keys).Distinct().Order().ToArray();
        var result = new Dictionary<int, MatrixHeader>();
        foreach (var column in columns)
        {
            var cells = headerRows.Where(row => row.Cells.TryGetValue(column, out var value) &&
                    !string.IsNullOrWhiteSpace(value))
                .Select(row => (row.SourceRow, Value: row.Cells[column].Trim()))
                .ToArray();
            if (cells.Length == 0) continue;
            var components = cells.Select(cell => cell.Value)
                .Where((value, index) => index == 0 ||
                    !value.Equals(cells[index - 1].Value, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            result[column] = new MatrixHeader(column, components,
                cells.Select(cell => locator(cell.SourceRow, column)).ToArray());
        }
        return result;
    }

    private static bool IsRateColumn(
        MatrixHeader header,
        IReadOnlyList<InventoryTableRow> rows,
        IReadOnlyDictionary<int, MatrixHeader> headers)
    {
        var normalized = Normalize(header.Hierarchy);
        if (NonRateMeasureTerms.Any(normalized.Contains)) return false;
        var values = rows.Select(row => row.Cells.GetValueOrDefault(header.Column))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToArray();
        if (values.Length == 0) return false;
        if (IsExplicitDimensionColumn(normalized, values)) return false;
        var commercial = values.Count(IsCommercialRateValue);
        if (commercial == 0 || commercial * 5 < values.Length * 4) return false;
        if (RateHeaderTerms.Any(normalized.Contains) ||
            DurationPattern().IsMatch(header.Hierarchy) ||
            TimeRangePattern().IsMatch(header.Hierarchy)) return true;
        return HasRowRateDimension(headers) && header.Column > headers.Keys.Min();
    }

    private static bool IsExplicitDimensionColumn(
        string header,
        IReadOnlyList<string> values)
    {
        if ((header.Contains("duration") || header.Contains("length")) &&
            values.All(value => DurationPattern().IsMatch(value))) return true;
        if ((header.Contains("daypart") || header.Contains("timeslot") ||
             header == "time") && values.All(value => TimeRangePattern().IsMatch(value)))
            return true;
        if ((header.Contains("date") || header.Contains("valid")) &&
            values.All(value => DatePattern().IsMatch(value))) return true;
        return false;
    }

    private static bool HasRowRateDimension(
        IReadOnlyDictionary<int, MatrixHeader> headers) => headers.Values.Any(header =>
    {
        var value = Normalize(header.Hierarchy);
        return value.Contains("time") || value.Contains("day") ||
            value.Contains("programme") || value.Contains("program") ||
            value.Contains("duration") || value.Contains("format");
    });

    private static bool IsCommercialRateValue(string value) =>
        InventoryMoneyParser.TryParse(value, out _, out _) ||
        value.Contains("rate on request", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("POR", StringComparison.OrdinalIgnoreCase);

    private static InventoryTableRow[] FillDown(
        IReadOnlyList<InventoryTableRow> dataRows,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns,
        Func<int, int, string> locator)
    {
        var previous = new Dictionary<int, (string Value, string Locator)>();
        var result = new List<InventoryTableRow>();
        foreach (var row in dataRows)
        {
            var values = row.Cells.ToDictionary(item => item.Key, item => item.Value);
            var locators = row.SourceLocators?.ToDictionary(item => item.Key, item => item.Value)
                ?? new Dictionary<int, string>();
            var transformations = row.Transformations?.ToDictionary(item => item.Key, item => item.Value)
                ?? new Dictionary<int, string>();
            ApplyInheritedCells(row.SourceRow, values, locators, transformations,
                previous, headers, rateColumns, locator);
            result.Add(new InventoryTableRow(row.SourceRow, values, locators, transformations));
        }
        return result.ToArray();
    }

    private static void ApplyInheritedCells(
        int row,
        Dictionary<int, string> values,
        Dictionary<int, string> locators,
        Dictionary<int, string> transformations,
        Dictionary<int, (string Value, string Locator)> previous,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns,
        Func<int, int, string> locator)
    {
        foreach (var header in headers.Values.Where(item => !rateColumns.Contains(item.Column)))
        {
            var leaf = Normalize(header.Components[^1]);
            if (!InheritableHeaders.Contains(leaf)) continue;
            if (values.TryGetValue(header.Column, out var current) && !string.IsNullOrWhiteSpace(current))
            {
                var source = locators.GetValueOrDefault(header.Column) ?? locator(row, header.Column);
                previous[header.Column] = (current.Trim(), source);
                locators[header.Column] = source;
            }
            else if (previous.TryGetValue(header.Column, out var inherited))
            {
                values[header.Column] = inherited.Value;
                locators[header.Column] = inherited.Locator;
                transformations[header.Column] = MasterDataCodes.InventoryTransformationTypes
                    .DerivedFromSourceContext;
            }
        }
    }

    private static void AddRow(
        List<InventoryExtractedRow> result,
        InventoryTableRow row,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns,
        int rowOffset,
        Func<int, string> rowLocator,
        Func<int, int, string> cellLocator,
        Func<int, int, decimal?>? confidence,
        Func<int, int, string?>? position)
    {
        var values = ProjectIdentityValues(row, headers, rateColumns);
        var variants = rateColumns.Order().Select(column =>
                CreateRateVariant(row, headers[column], cellLocator, position))
            .Where(variant => variant is not null).Cast<InventoryExtractedRateVariant>().ToArray();
        if (variants.Length == 0) return;
        AddPrimaryRate(values, variants[0]);
        EnsureName(values, row, headers, rateColumns);
        var fieldLocators = FieldLocators(row, headers, rateColumns, cellLocator);
        fieldLocators["rate"] = variants[0].SourceLocator;
        var transformations = FieldTransformations(row, headers, rateColumns);
        decimal? minimum = confidence is null ? null : row.Cells.Keys
            .Select(column => confidence(row.SourceRow, column))
            .Where(value => value.HasValue).Select(value => value!.Value)
            .DefaultIfEmpty().Min();
        result.Add(new InventoryExtractedRow(rowOffset + result.Count + 1,
            rowLocator(row.SourceRow), values,
            MasterDataCodes.InventoryExtractionMethods.Tabular,
            minimum == 0 ? null : minimum, fieldLocators,
            RateConfidences(variants, confidence),
            FieldTransformations: transformations,
            RateVariants: variants));
    }

    private static SortedDictionary<string, string> ProjectIdentityValues(
        InventoryTableRow row,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var cell in row.Cells.OrderBy(item => item.Key))
        {
            if (rateColumns.Contains(cell.Key) || string.IsNullOrWhiteSpace(cell.Value) ||
                !headers.TryGetValue(cell.Key, out var header)) continue;
            var key = Normalize(header.Components[^1]);
            if (key.Length > 0) values.TryAdd(key, cell.Value.Trim());
        }
        return values;
    }

    private static InventoryExtractedRateVariant? CreateRateVariant(
        InventoryTableRow row,
        MatrixHeader header,
        Func<int, int, string> locator,
        Func<int, int, string?>? position)
    {
        if (!row.Cells.TryGetValue(header.Column, out var raw) ||
            !IsCommercialRateValue(raw)) return null;
        var parsedCurrency = InventoryMoneyParser.TryParse(raw, out _, out var parsed)
            ? parsed
            : string.Empty;
        var hierarchy = header.Hierarchy;
        var duration = DurationPattern().Match(hierarchy);
        var dimensions = header.Components.Select((value, index) =>
                (Key: "header_" + (index + 1).ToString(CultureInfo.InvariantCulture), Value: value))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        return new InventoryExtractedRateVariant(raw.Trim(),
            locator(row.SourceRow, header.Column), hierarchy, header.Locators,
            position?.Invoke(row.SourceRow, header.Column), RateType(hierarchy),
            parsedCurrency.Length > 0 ? parsedCurrency : Currency(hierarchy),
            BuyingUnit(hierarchy), Validity(hierarchy), null, Geography(header),
            Daypart(hierarchy), Days(hierarchy),
            duration.Success && int.TryParse(duration.Groups["seconds"].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? seconds : null,
            dimensions);
    }

    private static void AddPrimaryRate(
        SortedDictionary<string, string> values,
        InventoryExtractedRateVariant rate)
    {
        values["rate"] = rate.RawValue;
        if (rate.Currency is not null) values["currency"] = rate.Currency;
        if (rate.RateType is not null) values["ratetype"] = rate.RateType;
        if (rate.Daypart is not null) values.TryAdd("daypart", rate.Daypart);
        if (rate.DurationSeconds.HasValue)
            values.TryAdd("spotlengthseconds", rate.DurationSeconds.Value.ToString(CultureInfo.InvariantCulture));
    }

    private static void EnsureName(
        IDictionary<string, string> values,
        InventoryTableRow row,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns)
    {
        if (values.Keys.Any(key => key is "name" or "product" or "productname" or
                "sitename" or "station" or "stationname")) return;
        var preferred = new[] { "programme", "program", "show", "element", "description", "daypart", "timeslot" };
        foreach (var key in preferred)
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                values["name"] = value;
                return;
            }
        var first = row.Cells.OrderBy(item => item.Key).FirstOrDefault(item =>
            !rateColumns.Contains(item.Key) && headers.ContainsKey(item.Key) &&
            !string.IsNullOrWhiteSpace(item.Value));
        if (!string.IsNullOrWhiteSpace(first.Value)) values["name"] = first.Value.Trim();
    }

    private static Dictionary<string, string> FieldLocators(
        InventoryTableRow row,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns,
        Func<int, int, string> locator) => row.Cells
        .Where(cell => !rateColumns.Contains(cell.Key) && headers.ContainsKey(cell.Key) &&
            !string.IsNullOrWhiteSpace(cell.Value))
        .GroupBy(cell => Normalize(headers[cell.Key].Components[^1]), StringComparer.Ordinal)
        .Where(group => group.Key.Length > 0)
        .ToDictionary(group => group.Key, group => row.SourceLocators?.GetValueOrDefault(group.First().Key)
            ?? locator(row.SourceRow, group.First().Key), StringComparer.Ordinal);

    private static Dictionary<string, string>? FieldTransformations(
        InventoryTableRow row,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlySet<int> rateColumns)
    {
        if (row.Transformations is null) return null;
        return row.Transformations.Where(item => !rateColumns.Contains(item.Key) && headers.ContainsKey(item.Key))
            .ToDictionary(item => Normalize(headers[item.Key].Components[^1]), item => item.Value,
                StringComparer.Ordinal);
    }

    private static Dictionary<string, decimal?>? RateConfidences(
        IReadOnlyList<InventoryExtractedRateVariant> variants,
        Func<int, int, decimal?>? confidence) => null;

    private static string? RateType(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("cpm")) return MasterDataCodes.RateTypes.Cpm;
        if (normalized.Contains("package")) return MasterDataCodes.RateTypes.PackageRate;
        if (normalized.Contains("spot") || DurationPattern().IsMatch(value))
            return MasterDataCodes.RateTypes.SpotRate;
        return null;
    }

    private static string? Currency(string value) =>
        value.Contains("ZAR", StringComparison.OrdinalIgnoreCase) ||
        Regex.IsMatch(value, @"(?:^|\s)R(?:\s|$)", RegexOptions.CultureInvariant)
            ? MasterDataCodes.Currencies.Zar : null;

    private static string? BuyingUnit(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("cpm")) return "THOUSAND_IMPRESSIONS";
        if (normalized.Contains("spot") || DurationPattern().IsMatch(value)) return "SPOT";
        if (normalized.Contains("month")) return "MONTH";
        if (normalized.Contains("week")) return "WEEK";
        return null;
    }

    private static DateOnly? Validity(string value) =>
        DateOnly.TryParse(DatePattern().Match(value).Value, CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces, out var date) ? date : null;

    private static string? Geography(MatrixHeader header) => header.Components.Any(component =>
        Normalize(component) is "region" or "province" or "geography" or "market" or "area")
        ? header.Components[^1] : null;

    private static string? Daypart(string value) => TimeRangePattern().Match(value) is { Success: true } match
        ? match.Value : null;

    private static string? Days(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("weekend")) return "WEEKEND";
        if (normalized.Contains("weekday") || normalized.Contains("mondayfriday")) return "WEEKDAY";
        return null;
    }

    private static string Normalize(string value) =>
        InventoryTabularProjection.NormalizeHeader(value);

    private sealed record MatrixHeader(
        int Column,
        IReadOnlyList<string> Components,
        IReadOnlyList<string> Locators)
    {
        internal string Hierarchy => string.Join(" > ", Components);
    }
}
