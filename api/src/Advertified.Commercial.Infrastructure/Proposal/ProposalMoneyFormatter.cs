using System.Globalization;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Proposal;

internal static class ProposalMoneyFormatter
{
    internal static string Format(long amountMinor, string currency)
    {
        var minorUnitDigits = CurrencyMetadata.RequireMinorUnitDigits(currency);
        var scale = CurrencyMetadata.MinorUnitScale(minorUnitDigits);
        var major = Math.DivRem(amountMinor, scale, out var minor);
        var formattedMajor = major.ToString("N0", CultureInfo.InvariantCulture);
        return minor == 0 || minorUnitDigits == 0
            ? $"{currency} {formattedMajor}"
            : $"{currency} {formattedMajor}.{minor.ToString(
                $"D{minorUnitDigits}", CultureInfo.InvariantCulture)}";
    }
}
