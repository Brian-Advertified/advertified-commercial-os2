using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class DoclingInventoryProjection
{
    [GeneratedRegex(
        @"(?im)^\s*(?<supplier>[\p{L}\p{N}][^\r\n:]{1,180}?)\s+(?:digital\s+)?rate\s+card\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RateCardTitlePattern();

    private static void AddRateCardTitleContext(
        TextItem item,
        SortedDictionary<string, string> values,
        Dictionary<string, string> locators)
    {
        var match = RateCardTitlePattern().Match(item.Text);
        if (!match.Success)
            return;
        var supplier = match.Groups["supplier"].Value.Trim();
        if (supplier.Length == 0 ||
            !values.TryAdd("supplier", supplier))
        {
            return;
        }
        locators["supplier"] =
            "docling:page=" + item.Page +
            ";text=" + item.Number +
            ";rate-card-title=1";
    }
}
