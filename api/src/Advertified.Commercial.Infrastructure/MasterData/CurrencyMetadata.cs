using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.MasterData;

internal sealed record CurrencyDefinition(
    string Code,
    int MinorUnitDigits,
    string[] BriefMarkers);

internal static class CurrencyMetadata
{
    private const string MinorUnitDigitsField = "minorUnitDigits";
    private const string BriefMarkersField = "briefMarkers";
    private static readonly Lazy<IReadOnlyDictionary<string, int>> ActiveMinorUnitDigits =
        new(() => ReadActive(MasterDataRegistryReader.Read()).ToDictionary(
            item => item.Code,
            item => item.MinorUnitDigits,
            StringComparer.Ordinal));

    internal static CurrencyDefinition[] ReadActive(MasterDataRegistry registry)
    {
        var currencies = registry.Collections.Single(collection =>
            collection.Code == MasterDataCodes.Currencies.Collection);
        var definitions = currencies.Items
            .Where(item => item.IsActive)
            .Select(Read)
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length == 0)
        {
            throw new InvalidOperationException(
                "The governed active-currency collection is empty.");
        }
        return definitions;
    }

    internal static long? MajorToMinor(decimal amount, int minorUnitDigits)
    {
        if (amount < 0m || minorUnitDigits is < 0 or > 9) return null;
        try
        {
            var scale = MinorUnitScale(minorUnitDigits);
            var minor = decimal.Round(
                amount * scale, 0, MidpointRounding.AwayFromZero);
            return minor > long.MaxValue ? null : (long)minor;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    internal static bool TryGetMinorUnitDigits(string currency, out int digits) =>
        ActiveMinorUnitDigits.Value.TryGetValue(currency, out digits);

    internal static int RequireMinorUnitDigits(string currency) =>
        TryGetMinorUnitDigits(currency, out var digits)
            ? digits
            : throw new InvalidOperationException(
                "The currency has no active governed minor-unit definition.");

    internal static long MinorUnitScale(int minorUnitDigits)
    {
        if (minorUnitDigits is < 0 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnitDigits));
        }
        var scale = 1L;
        for (var digit = 0; digit < minorUnitDigits; digit++) scale *= 10L;
        return scale;
    }

    private static CurrencyDefinition Read(MasterDataRegistryItem item)
    {
        using var metadata = JsonDocument.Parse(item.MetadataJson);
        var root = metadata.RootElement;
        var digits = root.TryGetProperty(MinorUnitDigitsField, out var digitsValue) &&
            digitsValue.TryGetInt32(out var parsedDigits) ? parsedDigits : -1;
        var markers = root.TryGetProperty(BriefMarkersField, out var markerValues)
            ? markerValues.EnumerateArray()
                .Select(value => value.GetString()?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        if (digits is < 0 or > 9 || markers.Length == 0)
        {
            throw new InvalidOperationException(
                $"The governed currency {item.Code} has incomplete parsing metadata.");
        }
        return new CurrencyDefinition(item.Code, digits, markers);
    }
}
