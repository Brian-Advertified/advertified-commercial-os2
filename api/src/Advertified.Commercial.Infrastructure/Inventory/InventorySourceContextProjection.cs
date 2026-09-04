using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySourceContextProjection
{
    private const string FileNameLocator = "source:file-name";

    internal static InventoryExtractionResult Apply(
        InventoryExtractionRequest request,
        InventoryExtractionResult extraction)
    {
        var supplier = InferSupplier(request.FileName);
        var channel = InferChannel(request.FileName);
        if (supplier is null && channel is null)
            return extraction;
        var rows = extraction.Rows
            .Select(row => Apply(row, supplier, channel))
            .ToArray();
        return InventoryExtractionContract.Create(
            extraction.AdapterCode,
            extraction.AdapterVersion,
            extraction.SchemaVersion,
            extraction.SourceHash,
            extraction.ProviderJson,
            rows);
    }

    internal static string? InferChannel(string fileName)
    {
        var value = Path.GetFileNameWithoutExtension(fileName);
        var outdoor = Regex.IsMatch(
            value,
            @"\bOOH\b|outdoor|billboard|roadside|site\s+inventory|screens?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var digital = Regex.IsMatch(
            value,
            @"digital|programmatic|\bDOOH\b|screens?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (outdoor || Regex.IsMatch(
                value,
                @"^RSD\s+Rate\s+Cards",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return digital || value.StartsWith(
                    "RSD", StringComparison.OrdinalIgnoreCase)
                ? MasterDataCodes.Channels.Dooh
                : MasterDataCodes.Channels.Ooh;
        }
        if (Regex.IsMatch(
                value,
                @"\bFM\b|radio|broadcasting",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
            !Regex.IsMatch(
                value, @"\bTV\b", RegexOptions.IgnoreCase |
                RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Radio;
        }
        if (Regex.IsMatch(
                value,
                @"\bTV\b|television|eMedia|Home\s+Channel|Ignition",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Tv;
        }
        if (Regex.IsMatch(
                value,
                @"^Arena-|Business\s+Day|Daily\s+Dispatch|Sowetan|Sunday\s+Times|The\s+Herald",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Print;
        }
        if (Regex.IsMatch(
                value,
                @"DMS\s+Digital|Digital\s+Rates|Publisher\s+Media\s+Kit",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Digital;
        }
        if (Regex.IsMatch(
                value,
                @"Virgin\s+Active",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return MasterDataCodes.Channels.Retail;
        }
        return null;
    }

    internal static string? InferSupplier(string fileName)
    {
        var value = Path.GetFileNameWithoutExtension(fileName)
            .Replace('_', ' ')
            .Trim();
        var known = new (string Pattern, string Supplier)[]
        {
            (@"^Algoa\s+FM\b", "Algoa FM"),
            (@"^Arena[- ]", "Arena"),
            (@"^BlackSpace\b", "BlackSpace"),
            (@"^Business\s+Day\s+TV\b", "Business Day TV"),
            (@"^Direct\s+Kaya\b", "Kaya FM"),
            (@"^DMS\b", "DStv Media Sales"),
            (@"^eleven8\b", "eleven8"),
            (@"^eMedia\b", "eMedia"),
            (@"^Ignition\s+TV\b", "Ignition TV"),
            (@"^Insight\s+Outdoor\b", "Insight Outdoor ZA"),
            (@"^JAC\b", "JAC"),
            (@"^JCDecaux\b", "JCDecaux ZA"),
            (@"^Jit\s+Tv\b", "Jit TV"),
            (@"^Jozi\s+FM\b", "Jozi FM"),
            (@"^Kena\s+Outdoor\b", "Kena Outdoor"),
            (@"^MAMG\b", "MAMG"),
            (@"^Media\s+Deck\s+2026\b", "Volt.Africa"),
            (@"^Primedia\s+Broadcasting\b", "Primedia Broadcasting"),
            (@"^Primedia\s+Outdoor\b", "Primedia Outdoor ZA"),
            (@"^Relativ\s+Media\b", "Relativ Media ZA"),
            (@"^Reveel\b", "Reveel ZA"),
            (@"^RSD\b", "RSD"),
            (@"^SABC\b", "SABC"),
            (@"^SB\s+Outdoor\b", "SB Outdoor"),
            (@"^Smile\s+90[.]4FM\b", "Smile 90.4FM"),
            (@"^Summit\s+OOH\s+Media\b", "Summit OOH Media"),
            (@"^The\s+Home\s+Channel\b", "The Home Channel"),
            (@"^Virgin\s+Active\s+ZA\b", "Virgin Active ZA"),
        };
        return known.FirstOrDefault(item => Regex.IsMatch(
            value,
            item.Pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Supplier;
    }

    private static InventoryExtractedRow Apply(
        InventoryExtractedRow row,
        string? supplier,
        string? channel)
    {
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal),
            StringComparer.Ordinal);
        var locators = row.FieldLocators?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        var bases = row.FieldEvidenceBases?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        var transformations = row.FieldTransformations?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        Add(values, locators, bases, transformations,
            "supplier", supplier);
        Add(values, locators, bases, transformations,
            "channel", channel);
        return row with
        {
            Values = values,
            FieldLocators = locators.Count == 0 ? null : locators,
            FieldEvidenceBases = bases.Count == 0 ? null : bases,
            FieldTransformations = transformations.Count == 0
                ? null
                : transformations,
        };
    }

    private static void Add(
        SortedDictionary<string, string> values,
        Dictionary<string, string> locators,
        Dictionary<string, string> bases,
        Dictionary<string, string> transformations,
        string field,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !values.TryAdd(field, value))
        {
            return;
        }
        locators[field] = FileNameLocator;
        bases[field] = MasterDataCodes
            .InventoryEvidenceBases.DerivedPolicy;
        transformations[field] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
    }
}
