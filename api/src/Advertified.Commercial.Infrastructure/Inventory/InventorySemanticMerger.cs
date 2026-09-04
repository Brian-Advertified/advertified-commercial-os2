using System.Text.RegularExpressions;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticMerger
{
    private static readonly HashSet<string> EnrichmentFields =
    [
        "channel",
        "product_type",
        "description",
    ];

    private static readonly HashSet<string> EnrichmentTransformations =
    [
        MasterDataCodes.InventoryTransformationTypes.DerivedFromChannel,
        MasterDataCodes.InventoryTransformationTypes
            .DerivedFromSourceContext,
    ];

    [GeneratedRegex(
        @"\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    internal static IReadOnlyList<InventoryExtractedRow> Merge(
        IReadOnlyList<InventoryExtractedRow> sourceRows,
        IReadOnlyList<InventorySemanticPacket> packets,
        IReadOnlyList<AgentSemanticResult> results,
        InventoryCodeSets codes)
    {
        if (results.Count != packets.Count ||
            results.Select(result => result.InputHash)
                .Distinct(StringComparer.Ordinal).Count() != results.Count)
        {
            throw InvalidGrounding();
        }
        var rows = sourceRows.ToList();
        var packetsByHash = packets.ToDictionary(
            packet => packet.InputHash,
            StringComparer.Ordinal);
        var proposedFields = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var result in results)
        {
            if (!packetsByHash.TryGetValue(
                    result.InputHash, out var packet) ||
                packet.Operation !=
                    InventorySemanticOperations.SemanticEnrichment)
            {
                throw InvalidGrounding();
            }
            Validate(
                result.Response,
                packet,
                codes,
                proposedFields);
            foreach (var candidate in
                     result.Response.Artifact!.Candidates)
            {
                MergeCandidate(rows, candidate);
            }
        }
        return rows.Select((row, index) =>
            row with { Number = index + 1 }).ToArray();
    }

    private static void MergeCandidate(
        List<InventoryExtractedRow> rows,
        ProposedInventoryCandidate candidate)
    {
        var matches = rows
            .Select((row, index) => (row, index))
            .Where(item => string.Equals(
                item.row.Locator,
                candidate.SourceLocator,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            throw InvalidGrounding();
        var proposed = Project(candidate);
        rows[matches[0].index] = MergeRow(
            matches[0].row,
            proposed);
    }

    private static InventoryExtractedRow Project(
        ProposedInventoryCandidate candidate)
    {
        var values = new SortedDictionary<string, string>(
            StringComparer.Ordinal);
        var locators = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var confidences = new Dictionary<string, decimal?>(
            StringComparer.Ordinal);
        var bases = new Dictionary<string, string>(
            StringComparer.Ordinal);
        var transformations = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var field in candidate.Fields)
        {
            var key = InventoryTabularProjection
                .NormalizeHeader(field.FieldName);
            values.Add(key, field.NormalizedValue!);
            locators.Add(key, field.SourceLocator);
            confidences.Add(key, field.Confidence);
            bases.Add(key, field.EvidenceBasis);
            transformations.Add(key, field.Transformation);
        }
        if (candidate.AmbiguityNotes.Count > 0)
        {
            values["semanticambiguity"] =
                string.Join(" | ", candidate.AmbiguityNotes);
        }
        return new InventoryExtractedRow(
            1,
            candidate.SourceLocator,
            values,
            MasterDataCodes.InventoryExtractionMethods.AgentProposal,
            confidences.Values
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty(0)
                .Min(),
            locators,
            confidences,
            bases,
            transformations);
    }

    private static InventoryExtractedRow MergeRow(
        InventoryExtractedRow current,
        InventoryExtractedRow proposed)
    {
        var values = CopyRequired(current.Values);
        var locators = CopyOptional(current.FieldLocators);
        var confidences = current.FieldConfidences?
            .ToDictionary(
                item => item.Key,
                item => item.Value,
                StringComparer.Ordinal)
            ?? new Dictionary<string, decimal?>(
                StringComparer.Ordinal);
        var bases = CopyOptional(current.FieldEvidenceBases);
        var transformations = CopyOptional(
            current.FieldTransformations);
        foreach (var item in proposed.Values)
        {
            if (!values.TryAdd(item.Key, item.Value))
                continue;
            if (proposed.FieldLocators?.TryGetValue(
                    item.Key, out var locator) == true)
            {
                locators[item.Key] = locator;
            }
            if (proposed.FieldConfidences?.TryGetValue(
                    item.Key, out var confidence) == true)
            {
                confidences[item.Key] = confidence;
            }
            if (proposed.FieldEvidenceBases?.TryGetValue(
                    item.Key, out var basis) == true)
            {
                bases[item.Key] = basis;
            }
            if (proposed.FieldTransformations?.TryGetValue(
                    item.Key, out var transformation) == true)
            {
                transformations[item.Key] = transformation;
            }
        }
        return current with
        {
            Values = values,
            FieldLocators = locators,
            FieldConfidences = confidences,
            FieldEvidenceBases = bases,
            FieldTransformations = transformations,
        };
    }

    private static void ValidateCode(
        ProposedInventoryField field,
        InventoryCodeSets codes)
    {
        IReadOnlySet<string>? allowed =
            field.FieldName switch
            {
                "channel" => codes.Channels,
                "product_type" => codes.ProductTypes,
                _ => null,
            };
        if (allowed is not null &&
            !allowed.Contains(field.NormalizedValue!))
        {
            throw InvalidGrounding();
        }
    }

    private static SortedDictionary<string, string> CopyRequired(
        IReadOnlyDictionary<string, string> values) =>
        new(values.ToDictionary(
                item => item.Key,
                item => item.Value),
            StringComparer.Ordinal);

    private static Dictionary<string, string> CopyOptional(
        IReadOnlyDictionary<string, string>? values) =>
        values?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
        ?? new Dictionary<string, string>(
            StringComparer.Ordinal);

    private static string Comparable(string value) =>
        Whitespace().Replace(value, " ")
            .Trim()
            .ToUpperInvariant();

    private static InvalidOperationException InvalidGrounding() =>
        new("Semantic enrichment output is not source-grounded.");
}

internal sealed record AgentSemanticResult(
    string InputHash,
    AgentRuntimeResponse<
        InventorySemanticExtractionArtifact> Response);
