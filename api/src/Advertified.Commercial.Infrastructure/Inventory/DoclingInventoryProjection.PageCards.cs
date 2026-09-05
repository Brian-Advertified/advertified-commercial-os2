using System.Text.RegularExpressions;

using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    private static readonly HashSet<string> SiteCodeLabels =
        new(StringComparer.Ordinal)
        {
            "sitenumber", "siteid", "sitecode", "reference",
        };

    [GeneratedRegex(
        @"(?<latitude>-?\d{1,2}(?:[.,]\d+)?)\s*[,;]\s*(?<longitude>-?\d{1,3}(?:[.,]\d+)?)",
        RegexOptions.CultureInvariant)]
    private static partial Regex DecimalCoordinatePattern();

    private static InventoryExtractedRow[] ReadPageCards(
        IReadOnlyList<TextItem> texts,
        int rowOffset)
    {
        var result = new List<InventoryExtractedRow>();
        foreach (var page in texts.GroupBy(item => item.Page))
        {
            var row = ReadPageCard(
                page.OrderBy(item => item.Number).ToArray(),
                rowOffset + result.Count + 1);
            if (row is not null)
                result.Add(row);
        }
        return result.ToArray();
    }

    private static InventoryExtractedRow? ReadPageCard(
        TextItem[] items,
        int number)
    {
        var facts = items
            .Select(ReadPageFact)
            .Where(fact => fact is not null)
            .Select(fact => fact!)
            .ToArray();
        var siteCode = facts.FirstOrDefault(fact =>
            SiteCodeLabels.Contains(fact.Label));
        if (siteCode is null)
            return null;

        var state = new PageCardState();
        Set(
            state,
            "productcode",
            siteCode.Value,
            Locator(siteCode.Item));
        AddPageCardFacts(state, facts, items);
        var title = SiteTitle(items, siteCode.Item.Number);
        if (title is not null)
        {
            Set(state, "address", title, Locator(items[0]));
            Set(
                state,
                "name",
                siteCode.Value + " - " + title,
                Locator(siteCode.Item));
        }
        else
        {
            Set(
                state,
                "name",
                siteCode.Value,
                Locator(siteCode.Item));
        }
        return new InventoryExtractedRow(
            number,
            "docling:page=" + items[0].Page +
                ";site-card=1",
            state.Values,
            items.Any(item => item.Confidence.HasValue)
                ? MasterDataCodes.InventoryExtractionMethods.Ocr
                : MasterDataCodes.InventoryExtractionMethods.KeyValue,
            MinimumTextConfidence(items),
            state.Locators,
            state.Confidences);
    }

    private static void AddPageCardFacts(
        PageCardState state,
        IReadOnlyList<PageFact> facts,
        IReadOnlyList<TextItem> items)
    {
        foreach (var fact in facts)
        {
            var locator = Locator(fact.Item);
            switch (fact.Label)
            {
                case "size":
                case "dimensions":
                    Set(state, "dimensions", fact.Value, locator);
                    break;
                case "gpscoordinate":
                case "gpscoordinates":
                case "gps":
                case "coordinates":
                    AddDecimalCoordinates(
                        state, fact.Value, locator);
                    break;
                case "availability":
                    Set(state, "availability", fact.Value, locator);
                    break;
                case "audiencereach":
                case "reach":
                    Set(state, "audiencereach", fact.Value, locator);
                    break;
                case "impacts":
                case "impressions":
                    Set(
                        state, "audienceimpressions",
                        fact.Value, locator);
                    break;
                case "audience":
                    Set(
                        state, "audiencelsmsemsegments",
                        fact.Value, locator);
                    break;
                case "siteinfo":
                case "description":
                    Set(state, "description", fact.Value, locator);
                    break;
                case "illuminated":
                    Set(
                        state, "creativespecification",
                        "Illuminated: " + fact.Value, locator);
                    break;
                case "ratecard":
                case "rate":
                    AddRate(state, fact, items, preferred: false);
                    break;
                case "discountedrate":
                case "netrate":
                    AddRate(state, fact, items, preferred: true);
                    break;
            }
        }
    }

    private static void AddRate(
        PageCardState state,
        PageFact fact,
        IReadOnlyList<TextItem> items,
        bool preferred)
    {
        var inline = CurrencyRaw(fact.Value);
        var value = inline is null
            ? NearestCurrencyValue(items, fact.Item.Number)
            : new CurrencyFact(inline, fact.Item);
        if (value is null ||
            (!preferred && state.HasPreferredRate))
        {
            return;
        }
        state.Values["rate"] = value.Value.Raw;
        state.Locators["rate"] = Locator(value.Value.Item);
        state.Confidences["rate"] = value.Value.Item.Confidence;
        if (preferred)
            state.HasPreferredRate = true;
    }

    private static CurrencyFact? NearestCurrencyValue(
        IReadOnlyList<TextItem> items,
        int number) =>
        items.Select(CurrencyValue)
            .Where(value =>
                value is not null &&
                Math.Abs(value.Value.Item.Number - number) <= 3)
            .OrderBy(value =>
                Math.Abs(value!.Value.Item.Number - number))
            .FirstOrDefault();

    private static string? CurrencyRaw(string value)
    {
        var lines = value.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (InventoryMoneyParser.TryParse(
                    line, out _, out var currency) &&
                currency.Length > 0)
            {
                return line;
            }
        }
        return null;
    }

    private static CurrencyFact? CurrencyValue(TextItem item)
    {
        var value = CurrencyRaw(item.Text);
        return value is null
            ? null
            : new CurrencyFact(value, item);
    }

    private static void AddDecimalCoordinates(
        PageCardState state,
        string raw,
        string locator)
    {
        var match = DecimalCoordinatePattern().Match(raw);
        if (!match.Success)
            return;
        Set(
            state,
            "latitude",
            match.Groups["latitude"].Value.Replace(',', '.'),
            locator);
        Set(
            state,
            "longitude",
            match.Groups["longitude"].Value.Replace(',', '.'),
            locator);
    }

    private static PageFact? ReadPageFact(TextItem item)
    {
        var lines = item.Text.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
            return null;
        var separator = lines[0].IndexOf(':');
        var rawLabel = separator >= 0
            ? lines[0][..separator]
            : lines[0];
        var label = InventoryTabularProjection.NormalizeHeader(
            rawLabel);
        if (!RecognizedPageLabel(label))
            return null;
        var firstValue = separator >= 0
            ? lines[0][(separator + 1)..].Trim()
            : string.Empty;
        var value = string.Join(' ', new[] { firstValue }
            .Concat(lines.Skip(1))
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return new PageFact(label, value, item);
    }

    private static bool RecognizedPageLabel(string label) =>
        SiteCodeLabels.Contains(label) ||
        label is "size" or "dimensions" or
            "gpscoordinate" or "gpscoordinates" or
            "gps" or "coordinates" or
            "availability" or "audiencereach" or "reach" or
            "impacts" or "impressions" or "audience" or
            "siteinfo" or "description" or "illuminated" or
            "ratecard" or "rate" or "discountedrate" or
            "netrate";

    private static string? SiteTitle(
        IReadOnlyList<TextItem> items,
        int siteCodeNumber) =>
        items.Where(item => item.Number < siteCodeNumber)
            .Select(item => item.Text.Replace('\n', ' ').Trim())
            .Where(text =>
                text.Length is >= 5 and <= 220 &&
                !text.Contains(':'))
            .FirstOrDefault();

    private static decimal? MinimumTextConfidence(
        IEnumerable<TextItem> items)
    {
        var values = items
            .Where(item => item.Confidence.HasValue)
            .Select(item => item.Confidence!.Value)
            .ToArray();
        return values.Length == 0
            ? null
            : values.Min();
    }

    private static string Locator(TextItem item) =>
        "docling:page=" + item.Page +
        ";text=" + item.Number;

    private static void Set(
        PageCardState state,
        string key,
        string value,
        string locator)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 ||
            !state.Values.TryAdd(key, normalized))
        {
            return;
        }
        state.Locators[key] = locator;
    }

    private sealed class PageCardState
    {
        internal SortedDictionary<string, string> Values { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Locators { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, decimal?> Confidences { get; } =
            new(StringComparer.Ordinal);
        internal bool HasPreferredRate { get; set; }
    }

    private sealed record PageFact(
        string Label,
        string Value,
        TextItem Item);

    private readonly record struct CurrencyFact(
        string Raw,
        TextItem Item);
}
