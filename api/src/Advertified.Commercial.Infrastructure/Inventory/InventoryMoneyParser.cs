using System.Globalization;
using System.Text.RegularExpressions;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventoryMoneyParser
{
    [GeneratedRegex(
        @"^\s*(?:(?<currency>ZAR|R)\s*)?(?<amount>\d[\d\s.,\u00A0]*)(?:\s*(?<currency>ZAR|R))?\s*(?:\([^)]*\)|[^\d]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(
        @"^\s*(?:(?:ZAR|R)\s*)?\d[\d\s.\u00A0]*,\d{1,2}(?:\s*(?:ZAR|R))?\s*(?:\([^)]*\)|[^\d]*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmbiguousCommaAmountPattern();

    internal static bool IsAmbiguousTruncatedRate(string raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        AmbiguousCommaAmountPattern().IsMatch(raw);

    internal static bool TryParse(
        string raw,
        out decimal amount,
        out string currency)
    {
        amount = default;
        currency = string.Empty;
        var match = MoneyPattern().Match(raw);
        if (!match.Success)
            return false;
        var normalized = NormalizeNumber(
            match.Groups["amount"].Value);
        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amount) ||
            amount < 0)
        {
            return false;
        }
        currency = match.Groups["currency"].Success
            ? "ZAR"
            : string.Empty;
        return true;
    }

    private static string NormalizeNumber(string raw)
    {
        var compact = raw
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);
        var comma = compact.LastIndexOf(',');
        var dot = compact.LastIndexOf('.');
        var separator = Math.Max(comma, dot);
        if (separator < 0)
            return compact;
        var decimals = compact.Length - separator - 1;
        var decimalSeparator = decimals is 1 or 2
            ? separator
            : -1;
        var digits = new string(
            compact.Where(char.IsDigit).ToArray());
        return decimalSeparator < 0
            ? digits
            : digits.Insert(
                digits.Length - decimals,
                ".");
    }
}
