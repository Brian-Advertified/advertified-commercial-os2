using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class NativePresentationProjection
{
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
                @"^(?:geography|province|city|region)\s*:\s*\S",
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

    private static bool IsPortfolioHeading(
        string geography,
        string detail) =>
        Regex.IsMatch(
            geography + " " + detail,
            @"\b(?:network|portfolio|snapshot|coverage|programmatic|dynamic|capable|advertising|media)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
        (geography.Contains(
             "market", StringComparison.OrdinalIgnoreCase) &&
         detail.Contains(
             "market", StringComparison.OrdinalIgnoreCase));

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
