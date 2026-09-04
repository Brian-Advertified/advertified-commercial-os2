using System.Text.RegularExpressions;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryCandidateNormalizer
{
    [GeneratedRegex(
        @"^\s*(?<code>[A-Z]{2,6}\s*[-_/]?\s*\d{2,8}[A-Z]?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingProductCodePattern();

    private static void ApplyProductCodeContext(
        Dictionary<string, string> canonical,
        Dictionary<string, (string Header, string Value)> sources)
    {
        if (canonical.ContainsKey("product_code") ||
            !canonical.TryGetValue("name", out var name))
        {
            return;
        }

        var match = LeadingProductCodePattern().Match(name);
        if (!match.Success)
            return;

        var rawCode = match.Groups["code"].Value.Trim();
        var code = Regex.Replace(rawCode, @"\s+", string.Empty)
            .Replace('_', '-')
            .Replace('/', '-')
            .ToUpperInvariant();
        if (code.Length < 4)
            return;

        canonical["product_code"] = code;
        sources["product_code"] = sources.TryGetValue("name", out var source)
            ? source
            : ("name", rawCode);
    }
}
