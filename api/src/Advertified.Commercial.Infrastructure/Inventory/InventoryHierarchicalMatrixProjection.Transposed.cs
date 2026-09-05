using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryHierarchicalMatrixProjection
{
    private static readonly string[] RowDimensionTerms =
    [
        "duration", "length", "daypart", "timeslot", "time", "day",
        "geography", "region", "province", "market", "area", "station",
        "channel", "publication", "format", "placement",
    ];

    private static bool IsTransposedMatrix(
        IReadOnlyDictionary<int, MatrixHeader> headers,
        HashSet<int> rateColumns)
    {
        var dimensions = headers.Values
            .Where(header => !rateColumns.Contains(header.Column)).ToArray();
        return dimensions.Length > 0 && dimensions.All(header =>
            RowDimensionTerms.Any(Normalize(header.Hierarchy).Contains));
    }

    private static InventoryExtractedRow[] ProjectTransposed(
        IReadOnlyList<InventoryTableRow> rows,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        HashSet<int> rateColumns,
        int rowOffset,
        Func<int, int, string> cellLocator,
        Func<int, int, decimal?>? confidence,
        Func<int, int, string?>? position)
    {
        var dimensionColumns = headers.Keys
            .Where(column => !rateColumns.Contains(column)).Order().ToArray();
        var dimensionRows = FillTransposedDimensions(
            rows, dimensionColumns, cellLocator);
        var result = new List<InventoryExtractedRow>();
        foreach (var column in rateColumns.Order())
        {
            var variants = dimensionRows.Select(row => CreateTransposedRate(
                    row, column, headers, dimensionColumns, cellLocator, position))
                .Where(rate => rate is not null)
                .Cast<InventoryExtractedRateVariant>().ToArray();
            if (variants.Length == 0) continue;
            result.Add(CreateTransposedProduct(result.Count + rowOffset + 1,
                headers[column], variants, rows, column, confidence));
        }
        return result.ToArray();
    }

    private static InventoryTableRow[] FillTransposedDimensions(
        IReadOnlyList<InventoryTableRow> rows,
        IReadOnlyList<int> dimensionColumns,
        Func<int, int, string> locator)
    {
        var previous = new Dictionary<int, (string Value, string Locator)>();
        var result = new List<InventoryTableRow>();
        foreach (var row in rows)
        {
            var cells = row.Cells.ToDictionary(item => item.Key, item => item.Value);
            var locators = row.SourceLocators?.ToDictionary(item => item.Key, item => item.Value)
                ?? new Dictionary<int, string>();
            var transformations = row.Transformations?.ToDictionary(item => item.Key, item => item.Value)
                ?? new Dictionary<int, string>();
            foreach (var column in dimensionColumns)
                FillTransposedDimension(row.SourceRow, column, cells,
                    locators, transformations, previous, locator);
            result.Add(new(row.SourceRow, cells, locators, transformations));
        }
        return result.ToArray();
    }

    private static void FillTransposedDimension(
        int row,
        int column,
        Dictionary<int, string> cells,
        Dictionary<int, string> locators,
        Dictionary<int, string> transformations,
        Dictionary<int, (string Value, string Locator)> previous,
        Func<int, int, string> locator)
    {
        if (cells.TryGetValue(column, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            var source = locators.GetValueOrDefault(column) ?? locator(row, column);
            previous[column] = (value.Trim(), source);
            locators[column] = source;
        }
        else if (previous.TryGetValue(column, out var inherited))
        {
            cells[column] = inherited.Value;
            locators[column] = inherited.Locator;
            transformations[column] = MasterDataCodes.InventoryTransformationTypes
                .DerivedFromSourceContext;
        }
    }

    private static InventoryExtractedRateVariant? CreateTransposedRate(
        InventoryTableRow row,
        int rateColumn,
        IReadOnlyDictionary<int, MatrixHeader> headers,
        IReadOnlyList<int> dimensionColumns,
        Func<int, int, string> locator,
        Func<int, int, string?>? position)
    {
        if (!row.Cells.TryGetValue(rateColumn, out var raw) ||
            !IsCommercialRateValue(raw)) return null;
        var dimensions = dimensionColumns.Where(row.Cells.ContainsKey)
            .Select(column => (Header: headers[column], Value: row.Cells[column]))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToArray();
        var hierarchy = string.Join(" > ", headers[rateColumn].Components.Concat(
            dimensions.Select(item => item.Header.Components[^1] + ": " + item.Value)));
        var headerLocators = headers[rateColumn].Locators.Concat(
                dimensions.SelectMany(item => item.Header.Locators.Append(
                    row.SourceLocators?.GetValueOrDefault(item.Header.Column) ??
                    locator(row.SourceRow, item.Header.Column))))
            .Distinct(StringComparer.Ordinal).ToArray();
        var values = headers[rateColumn].Components.Concat(
                dimensions.Select(item => item.Value))
            .Select((value, index) => new KeyValuePair<string, string>(
                "header_" + (index + 1).ToString(CultureInfo.InvariantCulture), value))
            .ToDictionary(item => item.Key, item => item.Value,
                StringComparer.Ordinal);
        var parsedCurrency = InventoryMoneyParser.TryParse(
            raw, out _, out var parsed) ? parsed : string.Empty;
        return new InventoryExtractedRateVariant(raw.Trim(),
            locator(row.SourceRow, rateColumn), hierarchy, headerLocators,
            position?.Invoke(row.SourceRow, rateColumn), RateType(hierarchy),
            parsedCurrency.Length > 0 ? parsedCurrency : Currency(hierarchy),
            BuyingUnit(hierarchy), Validity(hierarchy), null,
            Dimension(dimensions, "geography", "region", "province", "market", "area"),
            Daypart(hierarchy), Days(hierarchy), Duration(hierarchy), values);
    }

    private static InventoryExtractedRow CreateTransposedProduct(
        int number,
        MatrixHeader productHeader,
        InventoryExtractedRateVariant[] variants,
        IReadOnlyList<InventoryTableRow> rows,
        int column,
        Func<int, int, decimal?>? confidence)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = productHeader.Components[^1],
        };
        AddPrimaryRate(values, variants[0]);
        decimal? minimum = confidence is null ? null : rows
            .Select(row => confidence(row.SourceRow, column))
            .Where(value => value.HasValue).Select(value => value!.Value)
            .DefaultIfEmpty().Min();
        return new InventoryExtractedRow(number, productHeader.Locators[^1], values,
            MasterDataCodes.InventoryExtractionMethods.Tabular,
            minimum == 0 ? null : minimum,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = productHeader.Locators[^1],
                ["rate"] = variants[0].SourceLocator,
            }, RateVariants: variants);
    }

    private static string? Dimension(
        IEnumerable<(MatrixHeader Header, string Value)> dimensions,
        params string[] terms) => dimensions.FirstOrDefault(item => terms.Any(
            term => Normalize(item.Header.Hierarchy).Contains(term))).Value;

    private static int? Duration(string value)
    {
        var match = DurationPattern().Match(value);
        return match.Success && int.TryParse(match.Groups["seconds"].Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? seconds : null;
    }
}
