using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class NativePresentationProjection
{
    [GeneratedRegex(
        @"\b(?<code>[A-Z]{2,8}\s*[-/]?\s*\d{2,6}[A-Z]?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex PresentationSiteCodePattern();

    [GeneratedRegex(
        @"(?<width>\d+(?:[.,]\d+)?)\s*(?:m|cm|mm|ft)?\s*[x×]\s*(?<height>\d+(?:[.,]\d+)?)\s*(?:m|cm|mm|ft)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PresentationDimensionPattern();

    [GeneratedRegex(
        @"(?<latitude>-\d{1,2}(?:[.,]\d+)?)\s*[°]?\s*[,; ]+\s*(?<longitude>-?\d{1,3}(?:[.,]\d+)?)\s*[°]?",
        RegexOptions.CultureInvariant)]
    private static partial Regex PresentationCoordinatePattern();

    private static InventoryExtractedRow[] ReadSiteRows(
        XDocument slide,
        int slideNumber)
    {
        var shapes = ReadShapeValues(slide);
        if (shapes.Length == 0)
            return [];
        var coded = ReadCodedSite(shapes, slideNumber);
        if (coded is not null)
            return [coded];
        return ReadLocationCards(shapes, slideNumber);
    }

    private static InventoryExtractedRow? ReadCodedSite(
        IReadOnlyList<PresentationShapeValue> shapes,
        int slideNumber)
    {
        var codeShape = shapes
            .Select(shape => new
            {
                Shape = shape,
                Match = PresentationSiteCodePattern().Match(
                    shape.Text),
            })
            .FirstOrDefault(item =>
                item.Match.Success &&
                IsPlausibleSiteCode(
                    item.Match.Groups["code"].Value));
        if (codeShape is null)
            return null;
        if (!HasSiteEvidence(shapes))
            return null;

        var code = NormalizeSiteCode(
            codeShape.Match.Groups["code"].Value);
        var state = new PresentationSiteState();
        SetVisible(
            state,
            "productcode",
            code,
            ShapeLocator(slideNumber, codeShape.Shape.Number));
        AddAddressAndName(
            state, shapes, codeShape.Shape, code, slideNumber);
        AddDimensions(state, shapes, slideNumber);
        AddDescription(state, shapes, slideNumber);
        AddGeography(state, shapes, slideNumber);
        AddAvailability(state, shapes, slideNumber);
        AddFormatAndChannel(state, shapes, slideNumber);
        AddCoordinates(state, shapes, slideNumber);
        AddRate(state, shapes, slideNumber);
        return ToRow(state, slideNumber);
    }

    private static bool IsPlausibleSiteCode(string value)
    {
        var compact = NormalizeSiteCode(value);
        return !compact.StartsWith(
                   "FY", StringComparison.OrdinalIgnoreCase) &&
               !compact.StartsWith(
                   "Q", StringComparison.OrdinalIgnoreCase) &&
               compact.Length is >= 4 and <= 20;
    }

    private static bool HasSiteEvidence(
        IEnumerable<PresentationShapeValue> shapes) =>
        shapes.Any(shape =>
            PresentationDimensionPattern().IsMatch(shape.Text) ||
            PresentationCoordinatePattern().IsMatch(shape.Text) ||
            MoneyPattern().IsMatch(shape.Text) ||
            IsAvailability(shape.Text) ||
            IsMediaFormat(shape.Text) ||
            shape.Text.Length >= 80);

    private static void AddAddressAndName(
        PresentationSiteState state,
        IReadOnlyList<PresentationShapeValue> shapes,
        PresentationShapeValue codeShape,
        string code,
        int slideNumber)
    {
        var embedded = RemoveSiteCode(
            codeShape.Text, code);
        var addressShape = embedded.Length >= 4
            ? codeShape with { Text = embedded }
            : shapes.FirstOrDefault(shape =>
                shape.Number != codeShape.Number &&
                IsAddress(shape.Text));
        var address = addressShape?.Text.Trim();
        if (!string.IsNullOrWhiteSpace(address))
        {
            SetVisible(
                state,
                "address",
                address,
                ShapeLocator(slideNumber, addressShape!.Number));
            SetDerived(
                state,
                "name",
                code + " - " + address,
                ShapeLocator(slideNumber, addressShape.Number));
            return;
        }
        SetVisible(
            state,
            "name",
            code,
            ShapeLocator(slideNumber, codeShape.Number));
    }

    private static void AddDimensions(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        foreach (var shape in shapes)
        {
            var match = PresentationDimensionPattern().Match(
                shape.Text);
            if (!match.Success)
                continue;
            SetVisible(
                state,
                "dimensions",
                match.Value.Trim(),
                ShapeLocator(slideNumber, shape.Number));
            return;
        }
    }

    private static void AddDescription(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        var description = shapes
            .Where(shape =>
                shape.Text.Length is >= 80 and <= 2_000 &&
                !LooksLikeOcrNoise(shape.Text))
            .OrderByDescending(shape => shape.Text.Length)
            .FirstOrDefault();
        if (description is not null)
        {
            SetVisible(
                state,
                "description",
                description.Text,
                ShapeLocator(slideNumber, description.Number));
        }
    }

    private static void AddGeography(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        var values = shapes
            .Where(shape => IsGeography(shape.Text))
            .Select(shape => shape.Text.Replace('\n', ' ').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length == 0)
            return;
        var locator = shapes.First(shape =>
            values.Contains(
                shape.Text.Replace('\n', ' ').Trim(),
                StringComparer.OrdinalIgnoreCase));
        SetDerived(
            state,
            "geography",
            string.Join(" | ", values),
            ShapeLocator(slideNumber, locator.Number));
    }

    private static void AddAvailability(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        var value = shapes.FirstOrDefault(shape =>
            IsAvailability(shape.Text));
        if (value is not null)
        {
            SetVisible(
                state,
                "availability",
                value.Text,
                ShapeLocator(slideNumber, value.Number));
        }
    }

    private static void AddFormatAndChannel(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        var format = shapes.FirstOrDefault(shape =>
            IsMediaFormat(shape.Text));
        if (format is null)
            return;
        SetVisible(
            state,
            "format",
            format.Text,
            ShapeLocator(slideNumber, format.Number));
        var channel = Regex.IsMatch(
                format.Text,
                @"digital|screen|DOOH|programmatic",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? MasterDataCodes.Channels.Dooh
            : MasterDataCodes.Channels.Ooh;
        SetDerived(
            state,
            "channel",
            channel,
            ShapeLocator(slideNumber, format.Number));
    }

    private static void AddCoordinates(
        PresentationSiteState state,
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber)
    {
        foreach (var shape in shapes)
        {
            var match = PresentationCoordinatePattern().Match(
                shape.Text);
            if (!match.Success)
                continue;
            SetVisible(
                state,
                "latitude",
                match.Groups["latitude"].Value.Replace(',', '.'),
                ShapeLocator(slideNumber, shape.Number),
                MasterDataCodes.InventoryTransformationTypes.ParseDecimal);
            SetVisible(
                state,
                "longitude",
                match.Groups["longitude"].Value.Replace(',', '.'),
                ShapeLocator(slideNumber, shape.Number),
                MasterDataCodes.InventoryTransformationTypes.ParseDecimal);
            return;
        }
    }

    private static void AddRate(
        PresentationSiteState state,
        IReadOnlyList<PresentationShapeValue> shapes,
        int slideNumber)
    {
        for (var index = 0; index < shapes.Count; index++)
        {
            var match = MoneyPattern().Match(shapes[index].Text);
            if (!match.Success ||
                !InventoryMoneyParser.TryParse(
                    match.Value, out _, out var currency) ||
                currency.Length == 0)
            {
                continue;
            }
            var locator = ShapeLocator(
                slideNumber, shapes[index].Number);
            SetVisible(
                state,
                "rate",
                match.Value.Trim(),
                locator);
            var previous = index > 0
                ? shapes[index - 1].Text
                : string.Empty;
            if (Regex.IsMatch(
                    previous,
                    @"\bCPM\b",
                    RegexOptions.IgnoreCase |
                    RegexOptions.CultureInvariant))
            {
                SetDerived(
                    state,
                    "ratetype",
                    MasterDataCodes.RateTypes.Cpm,
                    locator);
            }
            return;
        }
    }

    private static InventoryExtractedRow[] ReadLocationCards(
        IEnumerable<PresentationShapeValue> shapes,
        int slideNumber) =>
        shapes.Select(shape => new
            {
                Shape = shape,
                Lines = shape.Text.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries),
            })
            .Where(item =>
                item.Lines.Length == 2 &&
                IsUpperLocation(item.Lines[0]) &&
                IsLocationDetail(item.Lines[1]))
            .Select((item, index) =>
            {
                var locator = ShapeLocator(
                    slideNumber, item.Shape.Number);
                var values = new SortedDictionary<string, string>(
                    StringComparer.Ordinal)
                {
                    ["address"] = item.Lines[1],
                    ["geography"] = item.Lines[0],
                    ["name"] = item.Lines[0] + " - " +
                        item.Lines[1],
                };
                var locators = values.Keys.ToDictionary(
                    key => key,
                    _ => locator,
                    StringComparer.Ordinal);
                return new InventoryExtractedRow(
                    index + 1,
                    locator,
                    values,
                    MasterDataCodes.InventoryExtractionMethods.KeyValue,
                    null,
                    locators);
            })
            .ToArray();

    private static InventoryExtractedRow ToRow(
        PresentationSiteState state,
        int slideNumber) => new(
        1,
        SlideLocator(slideNumber) + ";site-card=1",
        state.Values,
        MasterDataCodes.InventoryExtractionMethods.KeyValue,
        null,
        state.Locators,
        null,
        state.Bases,
        state.Transformations);

    private static PresentationShapeValue[] ReadShapeValues(
        XDocument slide)
    {
        var result = new List<PresentationShapeValue>();
        var number = 0;
        foreach (var shape in slide.Descendants(
                     Presentation + "sp"))
        {
            number++;
            var text = string.Join(
                "\n",
                shape.Descendants(Drawing + "p")
                    .Select(Text)
                    .Where(value => value.Length > 0));
            if (text.Length > 0)
                result.Add(new PresentationShapeValue(
                    number, Limit(text, 2_000)));
        }
        return result.ToArray();
    }

    private static bool IsAddress(string value) =>
        value.Length is >= 5 and <= 220 &&
        Regex.IsMatch(
            value,
            @"road|street|highway|freeway|mall|airport|rank|drive|avenue|route|interchange|CBD|off[- ]?ramp|centre|center",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
        !value.EndsWith(':');

    private static bool IsGeography(string value)
    {
        var normalized = value.Replace('\n', ' ').Trim();
        return normalized.Length <= 80 &&
            Regex.IsMatch(
                normalized,
                @"^(Gauteng|Western Cape|Eastern Cape|KwaZulu-Natal|Free State|Limpopo|Mpumalanga|North West|Northern Cape|Johannesburg|Cape Town|Durban|Pretoria|Tshwane|Soweto|Sandton|Bryanston|Fourways|Umhlanga|Ballito|JHB CBD)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsAvailability(string value) =>
        Regex.IsMatch(
            value.Trim(),
            @"^(immediate|immediately|available|limited|not available)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool IsMediaFormat(string value) =>
        value.Length <= 120 &&
        Regex.IsMatch(
            value,
            @"digital\s+(?:screen|billboard|pod)|static\s+(?:site|billboard)|billboard|DOOH|programmatic",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool LooksLikeOcrNoise(string value) =>
        value.Count(character => character == '\t') > 5 ||
        value.Count(character => character == '\\') > 5;

    private static bool IsUpperLocation(string value)
    {
        var letters = value.Where(char.IsLetter).ToArray();
        return letters.Length >= 3 &&
            letters.All(char.IsUpper) &&
            value.Length <= 80;
    }

    private static bool IsLocationDetail(string value) =>
        value.Length is >= 3 and <= 120 &&
        value.Any(char.IsLetter) &&
        !value.EndsWith(':');

    private static string RemoveSiteCode(
        string value,
        string code)
    {
        var pattern = Regex.Escape(code)
            .Replace("\\ ", @"\s*");
        return Regex.Replace(
                value,
                pattern,
                string.Empty,
                RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant)
            .Trim(' ', '-', ':', '–', '—');
    }

    private static string NormalizeSiteCode(string value) =>
        Regex.Replace(
            value.Trim().ToUpperInvariant(),
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);

    private static string ShapeLocator(
        int slide,
        int shape) =>
        SlideLocator(slide) +
        ";shape=" + shape.ToString(
            CultureInfo.InvariantCulture);

    private static void SetVisible(
        PresentationSiteState state,
        string key,
        string value,
        string locator,
        string? transformation = null)
    {
        if (!state.Values.TryAdd(key, value.Trim()))
            return;
        state.Locators[key] = locator;
        if (transformation is not null)
            state.Transformations[key] = transformation;
    }

    private static void SetDerived(
        PresentationSiteState state,
        string key,
        string value,
        string locator)
    {
        if (!state.Values.TryAdd(key, value.Trim()))
            return;
        state.Locators[key] = locator;
        state.Bases[key] = MasterDataCodes
            .InventoryEvidenceBases.DerivedPolicy;
        state.Transformations[key] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
    }

    private sealed class PresentationSiteState
    {
        internal SortedDictionary<string, string> Values { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Locators { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Bases { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, string> Transformations { get; } =
            new(StringComparer.Ordinal);
    }

    private sealed record PresentationShapeValue(
        int Number,
        string Text);
}
