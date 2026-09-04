using System.Text.Json;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private const string SourceFileLocator = "source:file-name";
    private static readonly HashSet<string> GlobalFields =
    [
        "supplier", "suppliername", "mediaowner", "mediaownername",
        "vatstatus", "vattreatment", "currency", "ratevalidfrom",
        "ratevalidto", "geography",
    ];

    [GeneratedRegex(
        @"(?im)\b(?<label>package[ \t]+cost|cost)[ \t]*:?[ \t]*(?<money>(?:ZAR|R)[ \t]*\d[\d \t.,\u00A0]*)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitCostPattern();

    [GeneratedRegex(
        @"(?<![A-Za-z])(?<money>(?:ZAR|R)[ \t]*\d[\d \t.,\u00A0]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(
        @"\b[A-Z]{2,5}[- ]?\d{3,6}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProductCodePattern();

    [GeneratedRegex(
        @"\b(?<date>(?:20\d{2}[/.-]\d{1,2}[/.-]\d{1,2}|\d{1,2}[/.-]\d{1,2}[/.-]20\d{2}|\d{1,2}[ \t]+[A-Za-z]{3,9}[ \t]+20\d{2}))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();

    [GeneratedRegex(
        @"^\s*\d{1,2}:\d{2}\s*",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingTimePattern();

    internal static List<InventoryExtractedRow> ReadRows(
        InventoryExtractionRequest request,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var texts = ReadTexts(root);
        var context = ReadContext(request, root);
        var rows = new List<InventoryExtractedRow>();
        rows.AddRange(ReadPageCards(texts, rows.Count));
        if (root.TryGetProperty("tables", out var tables) &&
            tables.ValueKind == JsonValueKind.Array)
        {
            var tableNumber = 0;
            foreach (var table in tables.EnumerateArray())
            {
                tableNumber++;
                rows.AddRange(ReadTable(
                    table, tableNumber, rows.Count, texts));
            }
        }
        rows.AddRange(ReadPricedTextLines(root, rows.Count));
        rows.AddRange(ReadExplicitOffers(root, rows.Count));
        if (!HasSellableRows(request, rows))
            rows.AddRange(ReadCatalogueText(root));
        return DeduplicateRows(rows)
            .Select((row, index) =>
                MergeContext(
                    row with { Number = index + 1 }, context))
            .ToList();
    }

    private static InventoryExtractedRow[] ReadExplicitOffers(
        JsonElement root,
        int rowOffset)
    {
        var texts = ReadTexts(root);
        var result = new List<InventoryExtractedRow>();
        for (var index = 0; index < texts.Length; index++)
        {
            var item = texts[index];
            foreach (Match match in ExplicitCostPattern().Matches(item.Text))
            {
                var money = CleanMoney(match.Groups["money"].Value);
                if (!InventoryMoneyParser.TryParse(
                        money, out _, out var currency) ||
                    currency.Length == 0)
                {
                    continue;
                }
                var previous =
                    index > 0 && texts[index - 1].Page == item.Page
                        ? texts[index - 1].Text
                        : string.Empty;
                var name = OfferName(item.Text[..match.Index], previous);
                if (name.Length == 0) continue;
                var packageRate = match.Groups["label"].Value.Contains(
                    "package", StringComparison.OrdinalIgnoreCase);
                var values = new SortedDictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["currency"] = currency,
                    ["description"] = Limit(item.Text, 2_000),
                    ["name"] = name,
                    ["rate"] = money,
                };
                if (packageRate)
                {
                    values["ratetype"] =
                        MasterDataCodes.RateTypes.PackageRate;
                }
                var locator = "docling:page=" + item.Page +
                    ";text=" + item.Number +
                    ";cost=" + (match.Index + 1);
                result.Add(new InventoryExtractedRow(
                    rowOffset + result.Count + 1,
                    locator,
                    values,
                    item.Confidence.HasValue
                        ? MasterDataCodes.InventoryExtractionMethods.Ocr
                        : MasterDataCodes.InventoryExtractionMethods.KeyValue,
                    item.Confidence,
                    values.Keys.ToDictionary(key => key, _ => locator),
                    values.Keys.ToDictionary(key => key, _ => item.Confidence),
                    packageRate
                        ? new Dictionary<string, string>
                        {
                            ["ratetype"] = MasterDataCodes
                                .InventoryEvidenceBases.DerivedPolicy,
                        }
                        : null,
                    packageRate
                        ? new Dictionary<string, string>
                        {
                            ["ratetype"] = MasterDataCodes
                                .InventoryTransformationTypes
                                .DerivedFromSourceContext,
                        }
                        : null));
            }
        }
        return result.ToArray();
    }

    private static InventoryExtractedRow[] ReadCatalogueText(
        JsonElement root)
    {
        var result = new List<InventoryExtractedRow>();
        foreach (var page in ReadTexts(root).GroupBy(item => item.Page))
        {
            var description = Limit(
                string.Join("\n", page.Select(item => item.Text)), 2_000);
            foreach (var item in page)
            {
                foreach (Match match in ProductCodePattern().Matches(item.Text))
                {
                    var code = match.Value.Trim();
                    var name = CatalogueName(item.Text, code);
                    if (name.Length == 0) name = code;
                    var locator = "docling:page=" + item.Page +
                        ";text=" + item.Number +
                        ";code=" + (match.Index + 1);
                    result.Add(new InventoryExtractedRow(
                        result.Count + 1,
                        locator,
                        new SortedDictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["description"] = description,
                            ["name"] = name,
                            ["productcode"] = code,
                        },
                        item.Confidence.HasValue
                            ? MasterDataCodes.InventoryExtractionMethods.Ocr
                            : MasterDataCodes.InventoryExtractionMethods.KeyValue,
                        item.Confidence,
                        new Dictionary<string, string>
                        {
                            ["description"] = locator,
                            ["name"] = locator,
                            ["productcode"] = locator,
                        }));
                }
            }
        }
        return result
            .GroupBy(
                row => (
                    row.Locator.Split(";code=", 2)[0],
                    row.Values["productcode"]),
                StringTupleComparer.Instance)
            .Select(group => group.First())
            .ToArray();
    }

    private static ProjectionContext ReadContext(
        InventoryExtractionRequest request,
        JsonElement root)
    {
        var values = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var locators = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var item in ReadTexts(root))
        {
            AddRateCardTitleContext(item, values, locators);
            var segmentNumber = 0;
            foreach (var segment in item.Text.Split(
                [';', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            {
                segmentNumber++;
                var separator = segment.IndexOf(':');
                if (separator <= 0 ||
                    separator == segment.Length - 1)
                {
                    continue;
                }
                var key = InventoryTabularProjection.NormalizeHeader(
                    segment[..separator]);
                if (!GlobalFields.Contains(key) ||
                    !values.TryAdd(
                        key, segment[(separator + 1)..].Trim()))
                {
                    continue;
                }
                locators[key] = "docling:page=" + item.Page +
                    ";text=" + item.Number +
                    ";segment=" + segmentNumber;
            }
        }
        var bases = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var transformations = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var channel = InferChannel(request.FileName);
        if (channel is not null && values.TryAdd("channel", channel))
        {
            locators["channel"] = SourceFileLocator;
            bases["channel"] =
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy;
            transformations["channel"] = MasterDataCodes
                .InventoryTransformationTypes.DerivedFromSourceContext;
        }
        return new ProjectionContext(
            values, locators, bases, transformations);
    }

    private static InventoryExtractedRow MergeContext(
        InventoryExtractedRow row,
        ProjectionContext context)
    {
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(
                item => item.Key, item => item.Value),
            StringComparer.Ordinal);
        foreach (var item in context.Values)
            values.TryAdd(item.Key, item.Value);
        return row with
        {
            Values = values,
            FieldLocators = Merge(
                row.FieldLocators, context.Locators),
            FieldEvidenceBases = Merge(
                row.FieldEvidenceBases, context.EvidenceBases),
            FieldTransformations = Merge(
                row.FieldTransformations, context.Transformations),
        };
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string>? first,
        IReadOnlyDictionary<string, string> second)
    {
        var result = first?.ToDictionary(
            item => item.Key, item => item.Value)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in second)
            result.TryAdd(item.Key, item.Value);
        return result;
    }

    private static bool TryMoney(
        string raw,
        out string money,
        out string currency)
    {
        var match = MoneyPattern().Match(raw);
        money = match.Success
            ? CleanMoney(match.Groups["money"].Value)
            : string.Empty;
        currency = string.Empty;
        return match.Success &&
            InventoryMoneyParser.TryParse(
                money, out _, out currency) &&
            currency.Length > 0;
    }

    private static string CleanMoney(string value) =>
        value.Trim().TrimEnd('.', ',');

    private static string SellableName(
        string raw,
        string money)
    {
        var value = raw.Replace(
            money, string.Empty,
            StringComparison.OrdinalIgnoreCase);
        return Limit(
            LeadingTimePattern().Replace(value, string.Empty)
                .Replace('\n', ' ')
                .Trim(' ', '-', ':', '|'),
            500);
    }

    private static string OfferName(
        string prefix,
        string previous)
    {
        var source = string.IsNullOrWhiteSpace(prefix)
            ? previous
            : prefix;
        return Limit(source.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries).LastOrDefault()
            ?? string.Empty, 500);
    }

    private static string CatalogueName(
        string text,
        string code)
    {
        var line = text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.Contains(
                code, StringComparison.OrdinalIgnoreCase));
        return Limit(
            (line ?? text).Replace(
                code, string.Empty,
                StringComparison.OrdinalIgnoreCase)
                .Trim(' ', '-', ':', '|'),
            500);
    }

    private static string? InferChannel(string fileName)
    {
        if (Regex.IsMatch(
                fileName,
                @"\bDOOH\b|digital\s+screens?",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Dooh;
        if (Regex.IsMatch(
                fileName,
                @"\bOOH\b|outdoor|billboard",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Ooh;
        if (Regex.IsMatch(
                fileName,
                @"\bFM\b|radio",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Radio;
        if (Regex.IsMatch(
                fileName,
                @"\bTV\b|television",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Tv;
        return null;
    }

    private static string Limit(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];
}
