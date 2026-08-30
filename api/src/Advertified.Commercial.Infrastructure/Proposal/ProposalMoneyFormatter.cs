using System.Globalization;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalMoneyFormatter
{
    internal static string Format(long amountMinor, string currency)
    {
        var major = Math.DivRem(amountMinor, 100, out var minor);
        var formattedMajor = major.ToString("N0", CultureInfo.InvariantCulture);
        return minor == 0
            ? $"{currency} {formattedMajor}"
            : $"{currency} {formattedMajor}.{minor:00}";
    }
}
