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
    int RowNumber,
    string? SupplierName);

internal static partial class InventoryCandidateNormalizer
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
    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        var extension = new Dictionary<string, string>(StringComparer.Ordinal);
        var evidence = new List<InventoryFieldEvidenceView>();
        var canonicalSources =
            new Dictionary<string, (string Header, string Value)>(StringComparer.Ordinal);
        foreach (var pair in row.Values)
        {
            if (Aliases.TryGetValue(pair.Key, out var field))
            {
                canonical[field] = pair.Value;
                canonicalSources[field] = (pair.Key, pair.Value);
            }
            else
            {
                extension[pair.Key] = pair.Value;
            }
        }
        evidence.AddRange(canonicalSources.Select(source => Evidence(
            source.Key,
            source.Value.Value,
            Normalize(source.Key, source.Value.Value, canonical),
            Transformation(source.Key, source.Value.Header),
            row.FieldLocators?.GetValueOrDefault(source.Value.Header) ?? row.Locator,
            sourceHash,
            capturedAtUtc,
            row.ExtractionMethod ?? MasterDataCodes.InventoryExtractionMethods.Tabular,
            row.FieldConfidences?.GetValueOrDefault(source.Value.Header) ?? row.Confidence)));
        var values = ToValues(
            canonical, extension, evidence, row.Locator, sourceHash, capturedAtUtc);
        return new ExtractedInventoryCandidate(
            values, evidence, row.Locator, row.Number,
            canonical.GetValueOrDefault("supplier_name")?.Trim());
    }

    private static InventoryCandidateValues ToValues(
        Dictionary<string, string> values,
        Dictionary<string, string> extension,
        List<InventoryFieldEvidenceView> evidence,
        string locator,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var channel = Code(values, "channel");
        var productType = Code(values, "product_type");
        if (productType is null && channel is not null && ProductTypes.TryGetValue(channel, out var derived))
        {
            productType = derived;
            evidence.Add(Evidence("product_type", channel, derived,
                MasterDataCodes.InventoryTransformationTypes.DerivedFromChannel,
                locator, sourceHash, capturedAtUtc,
                MasterDataCodes.InventoryExtractionMethods.PolicyDefault,
                evidenceBasis: MasterDataCodes.InventoryEvidenceBases.DerivedPolicy));
        }
        var availability = values.TryGetValue("availability", out var suppliedAvailability)
            ? AvailabilityCode(suppliedAvailability)
            : MasterDataCodes.AvailabilityStatuses.Available;
        if (!values.ContainsKey("availability"))
        {
            evidence.Add(Evidence("availability", null, availability,
                MasterDataCodes.InventoryTransformationTypes.ExplicitUnknown,
                "policy:inventory-availability-default-v1", sourceHash, capturedAtUtc,
                MasterDataCodes.InventoryExtractionMethods.PolicyDefault, 1m,
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy,
                MasterDataCodes.InventoryEvidenceStates.Verified,
                MasterDataCodes.InventoryEvidenceActions.None));
        }
        var audienceProfile = AudienceProfile(values);
        return InventoryCandidateValueNormalization.Normalize(new InventoryCandidateValues(
            Text(values, "product_code"), Text(values, "name"), channel, productType,
            Text(values, "geography"), Text(values, "address"),
            Decimal(values, "latitude"), Decimal(values, "longitude"),
            Code(values, "rate_type"), Code(values, "currency"), Rate(values),
            availability, extension, audienceProfile,
            Text(values, "description"), SupplierCommercial(values),
            SupplierContacts(values), CommercialTerms(values),
            Deliverable(values), Spatial(values), Package(values)));
    }

    private static InventoryAudienceProfileValues? AudienceProfile(
        Dictionary<string, string> values)
    {
        var profile = new InventoryAudienceProfileValues(
            Segments(values, "spoken_languages"),
            Segments(values, "understood_languages"),
            Segments(values, "life_stages"),
            Segments(values, "lsm_sem_segments"),
            Text(values, "audience_taxonomy"),
            Text(values, "audience_taxonomy_version"),
            Text(values, "audience_universe"),
            Text(values, "audience_measurement_source"),
            Text(values, "audience_measurement_period"),
            Text(values, "audience_methodology"),
            Text(values, "audience_limitations"),
            Measurements(values));
        return HasAudienceValue(profile) ? profile : null;
    }

    private static InventoryAudienceSegmentValue[] Segments(
        Dictionary<string, string> values,
        string field) => !values.TryGetValue(field, out var raw)
        ? []
        : raw.Split([';', '|', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseSegment).Where(item => item.Label.Length > 0).ToArray();

    private static InventoryAudienceSegmentValue ParseSegment(string value)
    {
        var text = value.Trim();
        var separator = Math.Max(text.LastIndexOf(':'), text.LastIndexOf('='));
        if (separator > 0 && TryPercent(text[(separator + 1)..], out var separated))
        {
            return new(text[..separator].Trim(), separated);
        }
        var percentStart = text.LastIndexOf(' ');
        return percentStart > 0 && TryPercent(text[(percentStart + 1)..], out var trailing)
            ? new(text[..percentStart].Trim(), trailing)
            : new(text, null);
    }

    private static bool TryPercent(string value, out decimal result) =>
        decimal.TryParse(value.Trim().TrimEnd('%'), NumberStyles.Number,
            CultureInfo.InvariantCulture, out result) && result is >= 0 and <= 100;

    private static bool HasAudienceValue(InventoryAudienceProfileValues profile) =>
        profile.SpokenLanguages.Count > 0 || profile.UnderstoodLanguages.Count > 0 ||
        profile.LifeStages.Count > 0 || profile.LsmSemSegments.Count > 0 ||
        profile.TaxonomyName is not null || profile.TaxonomyVersion is not null ||
        profile.Universe is not null || profile.MeasurementSource is not null ||
        profile.MeasurementPeriod is not null || profile.Methodology is not null ||
        profile.Limitations is not null || profile.Measurements?.Count > 0;

    private static InventoryAudienceMeasurementValue[] Measurements(
        Dictionary<string, string> values)
    {
        var definitions = new[]
        {
            (Field: "audience_reach", Metric: MasterDataCodes.PerformanceMetricTypes.Reach),
            (Field: "audience_listenership", Metric: MasterDataCodes.PerformanceMetricTypes.Listenership),
            (Field: "audience_footfall", Metric: MasterDataCodes.PerformanceMetricTypes.Footfall),
            (Field: "audience_impressions", Metric: MasterDataCodes.PerformanceMetricTypes.Impressions),
        };
        return definitions.Where(item => values.ContainsKey(item.Field))
            .Select(item => Measurement(values, item.Field, item.Metric)).ToArray();
    }

    private static InventoryAudienceMeasurementValue Measurement(
        Dictionary<string, string> values,
        string field,
        string metric)
    {
        var raw = values[field].Trim();
        var numeric = raw.EndsWith('%') ? raw[..^1] : raw;
        var parsed = decimal.TryParse(numeric, NumberStyles.Number,
            CultureInfo.InvariantCulture, out var value) ? value : (decimal?)null;
        var unit = Code(values, field + "_unit") ??
            (raw.EndsWith('%') ? MasterDataCodes.MeasurementUnits.Percent : null);
        return new(metric, parsed, unit, Text(values, "audience_universe"),
            Text(values, "audience_measurement_source"),
            Text(values, "audience_measurement_period"),
            Text(values, "audience_methodology"), Text(values, "audience_limitations"));
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
        "availability" => AvailabilityCode(value),
        "channel" or "product_type" or "rate_type" or "currency" =>
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
        string locator, string hash, DateTimeOffset capturedAtUtc,
        string extractionMethod, decimal? confidence = null,
        string? evidenceBasis = null, string? verificationState = null,
        string? requiredAction = null) =>
        new(field, raw, normalized, transformation, locator, hash,
            evidenceBasis ?? MasterDataCodes.InventoryEvidenceBases.SupplierSupplied,
            verificationState ?? MasterDataCodes.InventoryEvidenceStates.Unverified,
            requiredAction ?? MasterDataCodes.InventoryEvidenceActions.Review,
            capturedAtUtc, null, null, extractionMethod, confidence);

    private static string AvailabilityCode(string value)
    {
        var code = value.Trim().ToUpperInvariant().Replace(' ', '_');
        return code is "NOT_AVAILABLE" or "NOTAVAILABLE" or "BLACKOUT"
            ? MasterDataCodes.AvailabilityStatuses.Unavailable
            : code;
    }

    private static Dictionary<string, string> BuildAliases()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(result, "supplier_name", "supplier", "suppliername", "mediaowner",
            "mediaownername");
        Add(result, "product_code", "productcode", "code", "id", "siteid", "stationcode");
        Add(result, "name", "name", "product", "productname", "sitename", "station", "stationname");
        Add(result, "channel", "channel", "mediachannel", "mediatype");
        Add(result, "product_type", "producttype", "placementtype");
        Add(result, "geography", "geography", "location", "market", "area");
        Add(result, "address", "address", "siteaddress");
        Add(result, "latitude", "latitude", "lat");
        Add(result, "longitude", "longitude", "lon", "lng");
        Add(result, "rate_type", "ratetype", "pricingmodel");
        Add(result, "currency", "currency", "currencycode");
        Add(result, "rate_minor", "rateamountminor", "rateminor", "priceminor");
        Add(result, "rate", "rate", "price", "amount");
        Add(result, "availability", "availability", "availabilitystatus");
        Add(result, "spoken_languages", "language", "languages", "spokenlanguage",
            "spokenlanguages", "audiencelanguage", "audiencelanguages");
        Add(result, "understood_languages", "understoodlanguage", "understoodlanguages");
        Add(result, "life_stages", "lifestage", "lifestages", "agegroup", "agegroups");
        Add(result, "lsm_sem_segments", "lsm", "sem", "lsmsem", "lsmsegments",
            "semsegments", "lsmsemsegments");
        Add(result, "audience_taxonomy", "audiencetaxonomy", "segmenttaxonomy");
        Add(result, "audience_taxonomy_version", "audiencetaxonomyversion",
            "segmenttaxonomyversion", "lsmversion", "semversion");
        Add(result, "audience_universe", "audienceuniverse", "universe");
        Add(result, "audience_measurement_source", "audiencesource", "measurementsource",
            "researchsource");
        Add(result, "audience_measurement_period", "audienceperiod", "measurementperiod",
            "researchperiod");
        Add(result, "audience_methodology", "audiencemethodology", "measurementmethodology",
            "researchmethodology");
        Add(result, "audience_limitations", "audiencelimitations", "measurementlimitations");
        Add(result, "audience_reach", "reach", "audiencereach", "monthlyreach");
        Add(result, "audience_reach_unit", "reachunit", "audiencereachunit");
        Add(result, "audience_listenership", "listenership", "listeners", "monthlylisteners");
        Add(result, "audience_listenership_unit", "listenershipunit", "listenersunit");
        Add(result, "audience_footfall", "footfall", "monthlyfootfall", "dailyfootfall");
        Add(result, "audience_footfall_unit", "footfallunit");
        Add(result, "audience_impressions", "impressions", "estimatedimpressions");
        Add(result, "audience_impressions_unit", "impressionsunit");
        AddStructuredAliases(result);
        return result;
    }

    private static void Add(Dictionary<string, string> aliases, string field, params string[] values)
    {
        foreach (var value in values)
        {
            if (!aliases.TryAdd(value, field))
            {
                throw new InvalidOperationException($"Inventory alias '{value}' has multiple owners.");
            }
        }
    }
}
