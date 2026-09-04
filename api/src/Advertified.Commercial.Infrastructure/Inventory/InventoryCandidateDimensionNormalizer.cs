using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    [GeneratedRegex(
        @"^\s*(?<width>\d+(?:[.,]\d+)?)\s*(?:m|cm|mm|ft)?\s*(?:x|×|by|:|/|\s)\s*(?<height>\d+(?:[.,]\d+)?)\s*(?:m|cm|mm|ft)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DimensionPattern();

    private static void ApplyDimensionContext(
        Dictionary<string, string> values,
        Dictionary<string, (string Header, string Value)> sources)
    {
        if (!sources.TryGetValue("dimensions", out var source) ||
            source.Value.Trim() != "169" ||
            source.Header is not (
                "widthxheight" or "widthheight" or
                "aspectratio" or "aspect"))
        {
            return;
        }
        values["dimensions"] = "16 x 9";
    }

    private static string? NormalizedDimensions(
        IReadOnlyDictionary<string, string> values)
    {
        var raw = Text(values, "dimensions");
        return raw is null ? null : NormalizeDimensions(raw);
    }

    private static string NormalizeDimensions(string raw)
    {
        var match = DimensionPattern().Match(raw);
        return match.Success
            ? match.Groups["width"].Value + " x " +
                match.Groups["height"].Value
            : raw.Trim();
    }
}
