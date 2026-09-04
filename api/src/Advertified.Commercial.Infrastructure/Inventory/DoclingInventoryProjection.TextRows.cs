using System.Text.Json;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static InventoryExtractedRow[] ReadPricedTextLines(
        JsonElement root,
        int rowOffset)
    {
        var result = new List<InventoryExtractedRow>();
        foreach (var item in ReadTexts(root))
        {
            var lines = item.Text.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineNumber = lineIndex + 1;
                if (ExplicitCostPattern().IsMatch(line))
                    continue;
                var previousEnd = 0;
                foreach (Match match in MoneyPattern().Matches(line))
                {
                    var money = CleanMoney(
                        match.Groups["money"].Value);
                    if (!InventoryMoneyParser.TryParse(
                            money, out _, out var currency) ||
                        currency.Length == 0)
                    {
                        previousEnd = match.Index + match.Length;
                        continue;
                    }
                    var name = PricedName(
                        line, previousEnd, match.Index);
                    if (name is null &&
                        match.Index == 0 &&
                        lineIndex > 0)
                    {
                        name = PricedName(
                            lines[lineIndex - 1],
                            0,
                            lines[lineIndex - 1].Length);
                    }
                    previousEnd = match.Index + match.Length;
                    if (name is null) continue;

                    var locator = "docling:page=" + item.Page +
                        ";text=" + item.Number +
                        ";line=" + lineNumber +
                        ";price=" + (match.Index + 1);
                    var values = new SortedDictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["currency"] = currency,
                        ["name"] = name,
                        ["rate"] = money,
                    };
                    var bases =
                        new Dictionary<string, string>();
                    var transformations =
                        new Dictionary<string, string>();
                    var rateType = ExplicitRateType(line);
                    if (rateType is not null)
                    {
                        values["ratetype"] = rateType;
                        bases["ratetype"] = MasterDataCodes
                            .InventoryEvidenceBases.DerivedPolicy;
                        transformations["ratetype"] =
                            MasterDataCodes
                                .InventoryTransformationTypes
                                .DerivedFromSourceContext;
                    }
                    result.Add(new InventoryExtractedRow(
                        rowOffset + result.Count + 1,
                        locator,
                        values,
                        item.Confidence.HasValue
                            ? MasterDataCodes
                                .InventoryExtractionMethods.Ocr
                            : MasterDataCodes
                                .InventoryExtractionMethods.KeyValue,
                        item.Confidence,
                        values.Keys.ToDictionary(
                            key => key, _ => locator),
                        values.Keys.ToDictionary(
                            key => key, _ => item.Confidence),
                        bases,
                        transformations));
                }
            }
        }
        return result.ToArray();
    }

    private static string? PricedName(
        string line,
        int previousEnd,
        int moneyStart)
    {
        if (moneyStart <= previousEnd) return null;
        var source = line[previousEnd..moneyStart]
            .Trim(' ', '-', ':', '|', '•', '\t');
        var separator = source.LastIndexOfAny(
            ['|', ';', '•', '\t']);
        if (separator >= 0)
            source = source[(separator + 1)..].Trim();
        source = Regex.Replace(
            source,
            @"^\s*(?:\d+[.)-]?|[A-Z][.)-])\s*",
            string.Empty,
            RegexOptions.CultureInvariant);
        source = Limit(source.Trim(), 500);
        if (source.Length < 2 ||
            source.All(character =>
                !char.IsLetter(character)) ||
            IsCommercialHeading(source))
        {
            return null;
        }
        return source;
    }

    private static bool IsCommercialHeading(string value)
    {
        var normalized = InventoryTabularProjection
            .NormalizeHeader(value);
        return normalized is
            "vat" or
            "subtotal" or
            "total" or
            "terms" or
            "conditions" or
            "validity" or
            "rate" or
            "rates" or
            "price" or
            "cost";
    }

    private static string? ExplicitRateType(string line)
    {
        if (Regex.IsMatch(
                line, @"\bCPM\b|per\s+1[,. ]?000",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.RateTypes.Cpm;
        if (Regex.IsMatch(
                line, @"\bspot\b|per\s+(?:15|30|60)[- ]?sec",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.RateTypes.SpotRate;
        if (Regex.IsMatch(
                line, @"\bpackage\b|\bbundle\b",
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
            return MasterDataCodes.RateTypes.PackageRate;
        return null;
    }
}
