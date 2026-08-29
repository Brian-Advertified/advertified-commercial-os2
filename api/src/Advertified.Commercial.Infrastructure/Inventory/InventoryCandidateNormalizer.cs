using System.Globalization;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;

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
            [Gate6Channels.Ooh] = Gate6ProductTypes.OohSite,
            [Gate6Channels.Dooh] = Gate6ProductTypes.DoohScreen,
            [Gate6Channels.Radio] = Gate6ProductTypes.RadioSpot,
            [Gate6Channels.Television] = Gate6ProductTypes.TelevisionSpot,
            [Gate6Channels.Print] = Gate6ProductTypes.PrintPlacement,
            [Gate6Channels.Digital] = Gate6ProductTypes.DigitalPlacement,
            [Gate6Channels.Social] = Gate6ProductTypes.SocialPlacement,
            [Gate6Channels.Influencer] = Gate6ProductTypes.InfluencerPackage,
            [Gate6Channels.Experiential] = Gate6ProductTypes.Experience,
            [Gate6Channels.Podcast] = Gate6ProductTypes.PodcastSpot,
            [Gate6Channels.Retail] = Gate6ProductTypes.RetailPlacement,
            [Gate6Channels.Transit] = Gate6ProductTypes.TransitPlacement,
            [Gate6Channels.Mall] = Gate6ProductTypes.MallPlacement,
            [Gate6Channels.Email] = Gate6ProductTypes.EmailPlacement,
            [Gate6Channels.Mobile] = Gate6ProductTypes.MobilePlacement,
        };
    private static readonly string[] ExcludedClaims =
        ["audience", "reach", "listeners", "impressions", "ratings"];

    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row,
        string sourceHash)
    {
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var extension = new Dictionary<string, string>(StringComparer.Ordinal);
        var evidence = new List<InventoryFieldEvidenceView>();
        foreach (var pair in row.Values)
        {
            if (ExcludedClaims.Any(claim => pair.Key.Contains(claim, StringComparison.Ordinal)))
            {
                continue;
            }
            if (Aliases.TryGetValue(pair.Key, out var field))
            {
                canonical[field] = pair.Value;
                evidence.Add(Evidence(field, pair.Value, Normalize(field, pair.Value),
                    Transformation(field, pair.Key), row.Locator, sourceHash));
            }
            else
            {
                extension[pair.Key] = pair.Value;
            }
        }
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
                Gate6Transformations.DerivedFromChannel, locator, sourceHash));
        }
        var availability = Code(values, "availability") ?? Gate6Availability.Unknown;
        if (!values.ContainsKey("availability"))
        {
            evidence.Add(Evidence("availability", null, availability,
                Gate6Transformations.ExplicitUnknown, locator, sourceHash));
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
            ? checked((long)decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero))
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

    private static string? Normalize(string field, string value) => field switch
    {
        "channel" or "product_type" or "rate_type" or "currency" or "availability" =>
            value.Trim().ToUpperInvariant().Replace(' ', '_'),
        "rate" when decimal.TryParse(value, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var amount) =>
            checked((long)decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture),
        _ => value.Trim(),
    };

    private static string Transformation(string field, string header) => field switch
    {
        "rate" => Gate6Transformations.MajorToMinor,
        "latitude" or "longitude" => Gate6Transformations.ParseDecimal,
        "channel" or "product_type" or "rate_type" or "currency" or "availability" =>
            Gate6Transformations.UppercaseCode,
        _ => Gate6Transformations.Trim,
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
