using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record ExtractedInventoryCandidate(
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    string Locator,
    int RowNumber,
    string? SupplierName,
    bool HasDiscoveredSchema = false);

internal static partial class InventoryCandidateNormalizer
{
    private static readonly Dictionary<string, string> Aliases =
        BuildAliases();

    internal static IReadOnlySet<string> CanonicalMeanings { get; } =
        Aliases.Values.ToHashSet(StringComparer.Ordinal);

    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        if (row.DiscoveredFields is not null)
            return InventoryDiscoveredCandidateNormalizer.Normalize(row, sourceHash, capturedAtUtc);
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

    internal static InventoryCandidateValues ToValues(
        Dictionary<string, string> values,
        Dictionary<string, string> extension,
        List<InventoryFieldEvidenceView> evidence,
        string locator,
        string sourceHash,
        DateTimeOffset capturedAtUtc)
    {
        var channel = Code(values, "channel");
        var productType = Code(values, "product_type");
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
