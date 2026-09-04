using System.Text.Json;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static InventoryExtractedRow[] ReadAdjacentPricedTextBlocks(
        JsonElement root,
        int rowOffset,
        InventoryExtractedRow[] pageCards)
    {
        var result = new List<InventoryExtractedRow>();
        foreach (var page in ReadTexts(root)
                     .GroupBy(item => item.Page)
                     .OrderBy(group => group.Key))
        {
            var pagePrefix = "docling:page=" + page.Key + ";";
            if (pageCards.Any(row => row.Locator.StartsWith(
                    pagePrefix, StringComparison.Ordinal)))
            {
                continue;
            }
            var items = page.OrderBy(item => item.Number).ToArray();
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                var lines = item.Text.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    if (ExplicitCostPattern().IsMatch(line))
                        continue;
                    foreach (Match match in MoneyPattern().Matches(line))
                    {
                        if (IsRouteNumberPrice(line, match))
                            continue;
                        var raw = CleanMoney(match.Groups["money"].Value);
                        if (!InventoryMoneyParser.TryParse(
                                raw, out _, out var currency) ||
                            currency.Length == 0)
                        {
                            continue;
                        }
                        var name = AdjacentOfferName(
                            line,
                            match,
                            lines,
                            lineIndex,
                            items,
                            index);
                        if (name is null ||
                            IsNonOfferAdjacentContext(name, line))
                        {
                            continue;
                        }
                        var locator = "docling:page=" + item.Page +
                            ";text=" + item.Number +
                            ";line=" + (lineIndex + 1) +
                            ";adjacent-price=" + (match.Index + 1);
                        var values = new SortedDictionary<string, string>(
                            StringComparer.Ordinal)
                        {
                            ["currency"] = currency,
                            ["description"] = Limit(
                                name + " | " + line, 2_000),
                            ["name"] = Limit(name, 500),
                            ["rate"] = raw,
                        };
                        var bases = new Dictionary<string, string>(
                            StringComparer.Ordinal);
                        var transformations = new Dictionary<string, string>(
                            StringComparer.Ordinal);
                        var rateType = ExplicitRateType(
                            name + " " + line);
                        if (rateType is not null)
                        {
                            values["ratetype"] = rateType;
                            bases["ratetype"] = MasterDataCodes
                                .InventoryEvidenceBases.DerivedPolicy;
                            transformations["ratetype"] = MasterDataCodes
                                .InventoryTransformationTypes
                                .DerivedFromSourceContext;
                        }
                        result.Add(new InventoryExtractedRow(
                            rowOffset + result.Count + 1,
                            locator,
                            values,
                            item.Confidence.HasValue
                                ? MasterDataCodes.InventoryExtractionMethods.Ocr
                                : MasterDataCodes.InventoryExtractionMethods.KeyValue,
                            item.Confidence,
                            values.Keys.ToDictionary(
                                key => key, _ => locator,
                                StringComparer.Ordinal),
                            values.Keys.ToDictionary(
                                key => key, _ => item.Confidence,
                                StringComparer.Ordinal),
                            bases,
                            transformations));
                    }
                }
            }
        }
        return result.ToArray();
    }

    private static string? AdjacentOfferName(
        string line,
        Match match,
        string[] lines,
        int lineIndex,
        IReadOnlyList<TextItem> items,
        int itemIndex)
    {
        var prefix = line[..match.Index]
            .Trim(' ', '\t', ':', '-', '–', '—', '|', '•');
        if (IsUsefulAdjacentName(prefix))
            return prefix;
        for (var previousLine = lineIndex - 1;
             previousLine >= 0 && lineIndex - previousLine <= 3;
             previousLine--)
        {
            if (IsUsefulAdjacentName(lines[previousLine]))
                return lines[previousLine].Trim();
        }
        for (var previous = itemIndex - 1;
             previous >= 0 && itemIndex - previous <= 3;
             previous--)
        {
            var candidate = items[previous].Text.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .LastOrDefault();
            if (IsUsefulAdjacentName(candidate))
                return candidate!.Trim();
        }
        return null;
    }

    private static bool IsRouteNumberPrice(string line, Match match)
    {
        var token = string.Concat(match.Value.Where(
            character => !char.IsWhiteSpace(character)));
        if (!Regex.IsMatch(
                token,
                @"^R\d{1,3}$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }
        var suffix = line[(match.Index + match.Length)..].TrimStart();
        return Regex.IsMatch(
                   suffix,
                   @"^(?:/|\\|[-–—])|^(?:road|route|freeway|highway|intersection|interchange)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   line,
                   @"\b(?:road|route|freeway|highway|intersection|interchange)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsUsefulAdjacentName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 500 ||
            MoneyPattern().IsMatch(value))
        {
            return false;
        }
        var normalized = InventoryTabularProjection.NormalizeHeader(value);
        return normalized is not (
            "rate" or "rates" or "ratecard" or "price" or "cost" or
            "netrate" or "netrates" or "discountedrate" or "currency") &&
            value.Any(char.IsLetter);
    }

    private static bool IsNonOfferAdjacentContext(
        string name,
        string line)
    {
        var value = name + " " + line;
        return Regex.IsMatch(
            value,
            @"\b(?:telephone|phone|tel\.?|fax|vat\s*(?:number|no\.)|registration\s+number|liability|penalty)\b",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant);
    }
}
