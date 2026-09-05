using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryDiscoveredCandidateNormalizer
{
    internal const string UnresolvedMarker = "schemainterpretationunresolved";

    internal static ExtractedInventoryCandidate Normalize(
        InventoryExtractedRow row, string sourceHash, DateTimeOffset capturedAtUtc)
    {
        var fields = row.DiscoveredFields ?? throw new InventorySchemaRejectedException("Discovered evidence is missing.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var extension = new Dictionary<string, string>(StringComparer.Ordinal);
        var evidence = new List<InventoryFieldEvidenceView>();
        if (row.SchemaWarnings is { Count: > 0 })
            extension[UnresolvedMarker] = string.Join("\n", row.SchemaWarnings);
        var conflicts = fields.Where(field => field.CanonicalMeaning is not null)
            .GroupBy(field => field.CanonicalMeaning!)
            .Where(group => group.Select(field => field.InterpretedCode ?? field.RawValue).Distinct().Count() > 1)
            .Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.RawValue)) continue;
            var meaning = field.CanonicalMeaning;
            // Ambiguous monetary text is never coerced into a canonical rate.
            var ambiguousRate = meaning == "rate" &&
                InventoryMoneyParser.IsAmbiguousTruncatedRate(field.RawValue);
            if (ambiguousRate)
                extension["rateambiguity"] = "AMBIGUOUS_TRUNCATED_RATE";
            var valid = meaning is { } validMeaning &&
                InventoryCandidateNormalizer.CanonicalMeanings.Contains(validMeaning) && !conflicts.Contains(validMeaning) &&
                field.Warnings.Count == 0 && !ambiguousRate;
            if (valid)
            {
                values[meaning!] = field.InterpretedCode ?? field.RawValue;
            }
            else
            {
                extension[UnresolvedMarker] = "Unmapped, conflicting or ambiguous source evidence requires review.";
            }
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var item = FieldEvidence(field, values, conflicts, sourceHash, capturedAtUtc);
            if (!string.IsNullOrWhiteSpace(field.RawValue) && item.NormalizedValue is null)
                extension[UnresolvedMarker] = "Source evidence has no unambiguous canonical value.";
            // The relational field key is unique per candidate. Retain every source
            // citation while the artifact retains its full label and proposed meaning.
            if (field.CanonicalMeaning is null || !names.Add(item.FieldName))
                item = item with { FieldName = $"source:{evidence.Count:D4}" };
            evidence.Add(item);
        }
        var canonical = InventoryCandidateNormalizer.ToValues(values, extension, evidence,
            row.Locator, sourceHash, capturedAtUtc);
        return new ExtractedInventoryCandidate(canonical, evidence, row.Locator, row.Number,
            values.GetValueOrDefault("supplier_name"), HasDiscoveredSchema: true);
    }

    private static InventoryFieldEvidenceView FieldEvidence(InventoryDiscoveredField field,
        Dictionary<string, string> values, HashSet<string> conflicts, string sourceHash, DateTimeOffset now)
    {
        var meaning = field.CanonicalMeaning;
        var normalized = meaning is not null && values.TryGetValue(meaning, out var value) && !conflicts.Contains(meaning)
            ? InventoryCandidateNormalizer.NormalizeField(meaning, value, values) : null;
        // Exact raw label, interpretation and geometry also remain in the immutable discovered-fields artifact.
        return InventoryCandidateNormalizer.Evidence(meaning ?? "unresolved", field.RawValue, normalized,
            field.InterpretedCode is null ? MasterDataCodes.InventoryTransformationTypes.Trim :
                MasterDataCodes.InventoryTransformationTypes.DerivedFromSourceContext,
            field.SourceLocator, sourceHash, now, field.InterpretedCode is null
                ? MasterDataCodes.InventoryExtractionMethods.Tabular
                : MasterDataCodes.InventoryExtractionMethods.AgentProposal,
            field.Confidence, field.InterpretedCode is null
                ? MasterDataCodes.InventoryEvidenceBases.SupplierSupplied
                : MasterDataCodes.InventoryEvidenceBases.DerivedPolicy);
    }
}
