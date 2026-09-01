using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record ExtractedInventoryCandidate(
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    string Locator,
    int RowNumber);

internal static class InventoryCandidateNormalizer
{
    private static readonly Dictionary<string, string> Aliases = BuildAliases();
    private static readonly Dictionary<string, string> ProductTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MasterDataCodes.Channels.Ooh] = MasterDataCodes.InventoryProductTypes.OohSite,
            [MasterDataCodes.Channels.Dooh] = MasterDataCodes.InventoryProductTypes.DoohScreen,
            [MasterDataCodes.Channels.Radio] = MasterDataCodes.InventoryProductTypes.RadioSpot,
            [MasterDataCodes.Channels.Tv] = MasterDataCodes.InventoryProductTypes.TvSpot,
            [MasterDataCodes.Channels.Print] = MasterDataCodes.InventoryProductTypes.PrintPlacement,
            [MasterDataCodes.Channels.Digital] = MasterDataCodes.InventoryProductTypes.DigitalPlacement,
            [MasterDataCodes.Channels.Social] = MasterDataCodes.InventoryProductTypes.SocialPlacement,
            [MasterDataCodes.Channels.Influencer] = MasterDataCodes.InventoryProductTypes.InfluencerPackage,
            [MasterDataCodes.Channels.Experiential] = MasterDataCodes.InventoryProductTypes.Experience,
            [MasterDataCodes.Channels.Podcast] = MasterDataCodes.InventoryProductTypes.PodcastSpot,
            [MasterDataCodes.Channels.Retail] = MasterDataCodes.InventoryProductTypes.RetailPlacement,
            [MasterDataCodes.Channels.Transit] = MasterDataCodes.InventoryProductTypes.TransitPlacement,
            [MasterDataCodes.Channels.Mall] = MasterDataCodes.InventoryProductTypes.MallPlacement,
            [MasterDataCodes.Channels.Email] = MasterDataCodes.InventoryProductTypes.EmailPlacement,
            [MasterDataCodes.Channels.Mobile] = MasterDataCodes.InventoryProductTypes.MobilePlacement,
        };
    private static readonly string[] ExcludedClaims =
        [
            MasterDataCodes.InventoryUnsupportedClaimTerms.Audience,
            MasterDataCodes.InventoryUnsupportedClaimTerms.Reach,
            MasterDataCodes.InventoryUnsupportedClaimTerms.Listeners,
            MasterDataCodes.InventoryUnsupportedClaimTerms.Impressions,
            MasterDataCodes.InventoryUnsupportedClaimTerms.Ratings,
        ];

    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row,
        string sourceHash)
    {
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var extension = new Dictionary<string, string>(StringComparer.Ordinal);
        var evidence = new List<InventoryFieldEvidenceView>();
        var canonicalSources = new List<(string Header, string Field, string Value)>();
        foreach (var pair in row.Values)
        {
            if (ExcludedClaims.Any(claim => pair.Key.Contains(claim, StringComparison.Ordinal)))
            {
                continue;
            }
            if (Aliases.TryGetValue(pair.Key, out var field))
            {
                canonical[field] = pair.Value;
                canonicalSources.Add((pair.Key, field, pair.Value));
            }
            else
            {
                extension[pair.Key] = pair.Value;
            }
        }
        evidence.AddRange(canonicalSources.Select(source => Evidence(
            source.Field,
            source.Value,
            Normalize(source.Field, source.Value, canonical),
            Transformation(source.Field, source.Header),
            row.Locator,
            sourceHash)));
        var values = ToValues(canonical, extension, evidence, row.Locator, sourceHash);
        return new ExtractedInventoryCandidate(values, evidence, row.Locator, row.Number);
    }

    private static InventoryCandidateValues ToValues(
        Dictionary<string, string> values,
        Dictionary<string, string> extension,
        List<InventoryFieldEvidenceView> evidence,
        string locator,
        string sourceHash)
    {
        var channel = Code(values, "channel");
        var productType = Code(values, "product_type");
        if (productType is null && channel is not null && ProductTypes.TryGetValue(channel, out var derived))
        {
            productType = derived;
            evidence.Add(Evidence("product_type", channel, derived,
                MasterDataCodes.InventoryTransformationTypes.DerivedFromChannel, locator, sourceHash));
        }
        var availability = Code(values, "availability") ?? MasterDataCodes.AvailabilityStatuses.Unknown;
        if (!values.ContainsKey("availability"))
        {
            evidence.Add(Evidence("availability", null, availability,
                MasterDataCodes.InventoryTransformationTypes.ExplicitUnknown, locator, sourceHash));
        }
        return new InventoryCandidateValues(
            Text(values, "product_code"), Text(values, "name"), channel, productType,
            Text(values, "geography"), Text(values, "address"),
            Decimal(values, "latitude"), Decimal(values, "longitude"),
            Code(values, "rate_type"), Code(values, "currency"), Rate(values),
            availability, extension);
    }

    private static long? Rate(Dictionary<string, string> values)
    {
        if (values.TryGetValue("rate_minor", out var minor) &&
            long.TryParse(minor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var exact))
        {
            return exact;
        }
        return values.TryGetValue("rate", out var major) &&
            decimal.TryParse(major, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? MajorRateToMinor(amount, Code(values, "currency"))
            : null;
    }

    private static decimal? Decimal(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var raw) && decimal.TryParse(
            raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result : null;

    private static string? Text(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var value) && value.Trim().Length > 0 ? value.Trim() : null;

    private static string? Code(Dictionary<string, string> values, string field) =>
        Text(values, field)?.ToUpperInvariant().Replace(' ', '_');

    private static string? Normalize(
        string field,
        string value,
        Dictionary<string, string> values) => field switch
    {
        "channel" or "product_type" or "rate_type" or "currency" or "availability" =>
            value.Trim().ToUpperInvariant().Replace(' ', '_'),
        "rate" when decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var amount) =>
            MajorRateToMinor(amount, Code(values, "currency"))
                ?.ToString(CultureInfo.InvariantCulture),
        _ => value.Trim(),
    };

    private static long? MajorRateToMinor(decimal amount, string? currency) =>
        currency is not null && CurrencyMetadata.TryGetMinorUnitDigits(currency, out var digits)
            ? CurrencyMetadata.MajorToMinor(amount, digits)
            : null;

    private static string Transformation(string field, string header) => field switch
    {
        "rate" => MasterDataCodes.InventoryTransformationTypes.MajorToMinor,
        "latitude" or "longitude" => MasterDataCodes.InventoryTransformationTypes.ParseDecimal,
        "channel" or "product_type" or "rate_type" or "currency" or "availability" =>
            MasterDataCodes.InventoryTransformationTypes.UppercaseCode,
        _ => MasterDataCodes.InventoryTransformationTypes.Trim,
    };

    private static InventoryFieldEvidenceView Evidence(
        string field, string? raw, string? normalized, string transformation,
        string locator, string hash) =>
        new(field, raw, normalized, transformation, locator, hash);

    private static Dictionary<string, string> BuildAliases()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(result, "product_code", "productcode", "code", "id", "siteid", "stationcode");
        Add(result, "name", "name", "product", "productname", "sitename", "station", "stationname");
        Add(result, "channel", "channel", "mediachannel", "mediatype");
        Add(result, "product_type", "producttype", "format", "placementtype");
        Add(result, "geography", "geography", "location", "city", "market", "area");
        Add(result, "address", "address", "siteaddress");
        Add(result, "latitude", "latitude", "lat");
        Add(result, "longitude", "longitude", "lon", "lng");
        Add(result, "rate_type", "ratetype", "pricingmodel");
        Add(result, "currency", "currency", "currencycode");
        Add(result, "rate_minor", "rateamountminor", "rateminor", "priceminor");
        Add(result, "rate", "rate", "price", "amount");
        Add(result, "availability", "availability", "availabilitystatus");
        return result;
    }

    private static void Add(Dictionary<string, string> aliases, string field, params string[] values)
    {
        foreach (var value in values)
        {
            aliases[value] = field;
        }
    }
}
