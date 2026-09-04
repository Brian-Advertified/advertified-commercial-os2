using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryKnownSourcePolicy
{
    private const string FileNameLocator = "source:file-name";

    internal static InventoryExtractionResult Apply(
        InventoryExtractionRequest request,
        InventoryExtractionResult extraction)
    {
        var supplier = Supplier(request.FileName);
        var channel = Channel(request.FileName);
        var rows = extraction.Rows.Select(row =>
            Apply(row, supplier, channel, extraction.SourceHash)).ToArray();
        return InventoryExtractionContract.Create(
            extraction.AdapterCode,
            extraction.AdapterVersion,
            extraction.SchemaVersion,
            extraction.SourceHash,
            extraction.ProviderJson,
            rows);
    }

    private static InventoryExtractedRow Apply(
        InventoryExtractedRow row,
        string? supplier,
        string? channel,
        string sourceHash)
    {
        var values = new SortedDictionary<string, string>(
            row.Values.ToDictionary(item => item.Key, item => item.Value),
            StringComparer.Ordinal);
        var locators = Copy(row.FieldLocators);
        var bases = Copy(row.FieldEvidenceBases);
        var transformations = Copy(row.FieldTransformations);
        AddDerived(values, locators, bases, transformations,
            "supplier", supplier);
        AddDerived(values, locators, bases, transformations,
            "channel", channel);
        AddDerived(values, locators, bases, transformations,
            "producttype", ProductType(channel, values));
        if (!values.ContainsKey("productcode") &&
            HasSellableIdentity(values))
        {
            AddDerived(values, locators, bases, transformations,
                "productcode", CanonicalProductCode(
                    sourceHash, row.Locator, values));
        }
        if (!HasRate(values) && HasSellableIdentity(values))
        {
            AddDerived(values, locators, bases, transformations,
                "rateunknown", "RATE_ON_REQUEST");
        }
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

    private static string? Supplier(string fileName)
    {
        var mappings = new (string Pattern, string Supplier)[]
        {
            (@"\balgoa\s+fm\b", "Algoa FM"),
            (@"\barena[- ]|business day|daily dispatch|sowetan|sunday times|the herald", "Arena Holdings"),
            (@"blackspace", "BlackSpace"),
            (@"dstv|\bdms\b|digital rates & packages", "DStv Media Sales"),
            (@"digital screens concept", "Jit TV"),
            (@"eleven8", "Eleven8"),
            (@"emedia", "eMedia"),
            (@"ignition tv", "Ignition TV"),
            (@"insight outdoor", "Insight Outdoor ZA"),
            (@"\bjac\b|jacaranda", "Jacaranda FM"),
            (@"jcdecaux", "JCDecaux ZA"),
            (@"jit tv", "Jit TV"),
            (@"jozi fm", "Jozi FM"),
            (@"kena outdoor", "Kena Outdoor"),
            (@"mamg", "MAMG"),
            (@"media deck", "Volt Africa"),
            (@"primedia broadcasting", "Primedia Broadcasting"),
            (@"primedia outdoor", "Primedia Outdoor ZA"),
            (@"relativ media", "Relativ Media ZA"),
            (@"reveel", "Reveel"),
            (@"\brsd\b", "Roadside Digital"),
            (@"sabc", "SABC"),
            (@"sb outdoor", "SB Outdoor"),
            (@"smile\s*90", "Smile 90.4FM"),
            (@"summit ooh", "Summit OOH Media"),
            (@"home channel", "The Home Channel"),
            (@"virgin active", "Virgin Active"),
            (@"\by packages\b", "YFM"),
            (@"direct kaya|kaya packages", "Kaya 959"),
        };
        return mappings.FirstOrDefault(mapping => Regex.IsMatch(
            fileName,
            mapping.Pattern,
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant)).Supplier;
    }

    private static string? Channel(string fileName)
    {
        if (Regex.IsMatch(fileName,
                @"\bFM\b|radio|jac rate|kaya|y packages",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Radio;
        if (Regex.IsMatch(fileName,
                @"\bTV\b|television|home channel|emedia|ignition",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Tv;
        if (Regex.IsMatch(fileName,
                @"arena-|business day rate|daily dispatch|sowetan|sunday times|the herald",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Print;
        if (Regex.IsMatch(fileName,
                @"outdoor|\bOOH\b|billboard|roadside|site inventory|screens concept|jcdecaux|reveel|relativ|virgin active",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Ooh;
        if (Regex.IsMatch(fileName,
                @"digital|media deck|dstv|\bdms\b|eleven8|mamg",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return MasterDataCodes.Channels.Digital;
        return null;
    }

    private static string? ProductType(
        string? channel,
        SortedDictionary<string, string> values)
    {
        if (channel == MasterDataCodes.Channels.Radio)
            return MasterDataCodes.InventoryProductTypes.RadioSpot;
        if (channel == MasterDataCodes.Channels.Tv)
            return MasterDataCodes.InventoryProductTypes.TvSpot;
        if (channel == MasterDataCodes.Channels.Print)
            return MasterDataCodes.InventoryProductTypes.PrintPlacement;
        if (channel == MasterDataCodes.Channels.Digital)
            return MasterDataCodes.InventoryProductTypes.DigitalPlacement;
        if (channel == MasterDataCodes.Channels.Ooh)
        {
            var source = string.Join(' ', values.Values);
            return source.Contains("digital", StringComparison.OrdinalIgnoreCase) ||
                   source.Contains("screen", StringComparison.OrdinalIgnoreCase)
                ? MasterDataCodes.InventoryProductTypes.DoohScreen
                : MasterDataCodes.InventoryProductTypes.OohSite;
        }
        return null;
    }

    private static bool HasSellableIdentity(
        SortedDictionary<string, string> values) =>
        values.Keys.Any(key => key is
            "name" or "productcode" or "sitecode" or "sitenumber" or
            "platform" or "placement" or "programme" or "packagename");

    private static bool HasRate(
        SortedDictionary<string, string> values) =>
        values.ContainsKey("rate") ||
        values.ContainsKey("price") ||
        values.ContainsKey("cost") ||
        values.ContainsKey("baseprice") ||
        values.ContainsKey("rateunknown");

    private static string CanonicalProductCode(
        string sourceHash,
        string locator,
        SortedDictionary<string, string> values)
    {
        var identity = values.TryGetValue("name", out var name)
            ? name
            : string.Join('|', values
                .Where(item => item.Key is
                    "sitecode" or "sitenumber" or "platform" or
                    "placement" or "programme" or "packagename")
                .OrderBy(item => item.Key)
                .Select(item => item.Value));
        var material = sourceHash + "\n" + locator + "\n" + identity;
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        return "ADV-" + hash[..16];
    }

    private static Dictionary<string, string> Copy(
        IReadOnlyDictionary<string, string>? source) =>
        source?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal) ??
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static void AddDerived(
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
        bases[field] = MasterDataCodes.InventoryEvidenceBases.DerivedPolicy;
        transformations[field] = MasterDataCodes
            .InventoryTransformationTypes.DerivedFromSourceContext;
    }
}
