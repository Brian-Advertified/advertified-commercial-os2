using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record ExtractedInventoryCandidate(
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    string Locator,
    int RowNumber,
    string? SupplierName);

internal static partial class InventoryCandidateNormalizer
{
    private static readonly Dictionary<string, string> Aliases =
        BuildAliases();

    private static readonly Dictionary<string, string> ProductTypes =
        new(StringComparer.Ordinal)
        {
            [MasterDataCodes.Channels.Ooh] =
                MasterDataCodes.InventoryProductTypes.OohSite,
            [MasterDataCodes.Channels.Dooh] =
                MasterDataCodes.InventoryProductTypes.DoohScreen,
            [MasterDataCodes.Channels.Radio] =
                MasterDataCodes.InventoryProductTypes.RadioSpot,
            [MasterDataCodes.Channels.Tv] =
                MasterDataCodes.InventoryProductTypes.TvSpot,
            [MasterDataCodes.Channels.Print] =
                MasterDataCodes.InventoryProductTypes.PrintPlacement,
            [MasterDataCodes.Channels.Digital] =
                MasterDataCodes.InventoryProductTypes.DigitalPlacement,
            [MasterDataCodes.Channels.Social] =
                MasterDataCodes.InventoryProductTypes.SocialPlacement,
            [MasterDataCodes.Channels.Influencer] =
                MasterDataCodes.InventoryProductTypes.InfluencerPackage,
            [MasterDataCodes.Channels.Experiential] =
                MasterDataCodes.InventoryProductTypes.Experience,
            [MasterDataCodes.Channels.Podcast] =
                MasterDataCodes.InventoryProductTypes.PodcastSpot,
            [MasterDataCodes.Channels.Retail] =
                MasterDataCodes.InventoryProductTypes.RetailPlacement,
            [MasterDataCodes.Channels.Transit] =
                MasterDataCodes.InventoryProductTypes.TransitPlacement,
            [MasterDataCodes.Channels.Mall] =
                MasterDataCodes.InventoryProductTypes.MallPlacement,
            [MasterDataCodes.Channels.Email] =
                MasterDataCodes.InventoryProductTypes.EmailPlacement,
            [MasterDataCodes.Channels.Mobile] =
                MasterDataCodes.InventoryProductTypes.MobilePlacement,
        };

    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var canonical = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var extension = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var evidence = new List<InventoryFieldEvidenceView>();
        var sources =
            new Dictionary<string, (string Header, string Value)>(
                StringComparer.Ordinal);
        foreach (var pair in row.Values)
        {
            if (Aliases.TryGetValue(pair.Key, out var field))
            {
                canonical[field] = pair.Value;
                sources[field] = (pair.Key, pair.Value);
            }
            else
            {
                extension[pair.Key] = pair.Value;
            }
        }
        if (row.Values.TryGetValue("ratetype", out var explicitRateType))
        {
            canonical["rate_type"] = explicitRateType;
            sources["rate_type"] = ("ratetype", explicitRateType);
        }
        ApplyContextualMappings(row, canonical, sources);
        ApplyProductCodeContext(canonical, sources);
        ApplyKnownBrandCasing(canonical);
        ApplyDimensionContext(canonical, sources);
        if (canonical.TryGetValue("rate", out var rawRate) &&
            InventoryMoneyParser.IsAmbiguousTruncatedRate(rawRate))
        {
            extension["rateambiguity"] =
                "AMBIGUOUS_TRUNCATED_RATE";
        }
        evidence.AddRange(sources.Select(source => Evidence(
            source.Key,
            source.Value.Value,
            NormalizeField(
                source.Key,
                source.Value.Value,
                canonical),
            row.FieldTransformations?.GetValueOrDefault(
                source.Value.Header) ??
                Transformation(
                    source.Key,
                    source.Value.Header),
            row.FieldLocators?.GetValueOrDefault(
                source.Value.Header) ?? row.Locator,
            sourceHash,
            capturedAtUtc,
            row.ExtractionMethod ??
                MasterDataCodes.InventoryExtractionMethods.Tabular,
            row.FieldConfidences?.GetValueOrDefault(
                source.Value.Header) ?? row.Confidence,
            row.FieldEvidenceBases?.GetValueOrDefault(
                source.Value.Header))));
        var values = ToValues(
            canonical,
            extension,
            evidence,
            row.Locator,
            sourceHash,
            capturedAtUtc);
        return new ExtractedInventoryCandidate(
            values,
            evidence,
            row.Locator,
            row.Number,
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
        var channel = SourceChannel(
            values,
            evidence,
            locator,
            sourceHash,
            capturedAtUtc);
        var productType = Code(values, "product_type");
        if (productType is null &&
            channel is not null &&
            ProductTypes.TryGetValue(channel, out var derived))
        {
            productType = derived;
            evidence.Add(Evidence(
                "product_type",
                channel,
                derived,
                MasterDataCodes.InventoryTransformationTypes
                    .DerivedFromChannel,
                locator,
                sourceHash,
                capturedAtUtc,
                MasterDataCodes.InventoryExtractionMethods.PolicyDefault,
                evidenceBasis: MasterDataCodes
                    .InventoryEvidenceBases.DerivedPolicy));
        }
        var availability = Availability(
            values,
            evidence,
            sourceHash,
            capturedAtUtc);
        var rate = Rate(values);
        if (values.TryGetValue("rate", out var rawRate) &&
            InventoryMoneyParser.IsAmbiguousTruncatedRate(rawRate))
        {
            extension["rateambiguity"] =
                "AMBIGUOUS_TRUNCATED_RATE";
        }
        return InventoryCandidateValueNormalization.Normalize(
            new InventoryCandidateValues(
                Text(values, "product_code"),
                Text(values, "name"),
                channel,
                productType,
                Text(values, "geography"),
                Text(values, "address"),
                Decimal(values, "latitude"),
                Decimal(values, "longitude"),
                Text(values, "rate_type"),
                Currency(
                    values,
                    evidence,
                    locator,
                    sourceHash,
                    capturedAtUtc),
                rate,
                availability,
                extension,
                AudienceProfile(values),
                Text(values, "description"),
                SupplierCommercial(values),
                SupplierContacts(values),
                CommercialTerms(values),
                Deliverable(values),
                Spatial(values),
                Package(values)));
    }

    internal static bool RecognizesHeader(
        string normalizedHeader) =>
        Aliases.ContainsKey(normalizedHeader) ||
        normalizedHeader is "element" or "exposure" or "value";
}
