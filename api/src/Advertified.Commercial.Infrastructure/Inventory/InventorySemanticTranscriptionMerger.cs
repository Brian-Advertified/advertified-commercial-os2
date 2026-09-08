using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventorySemanticTranscriptionMerger
{
    private static readonly HashSet<string> RestrictedFields =
    [
        "channel", "producttype", "description", "ratetype",
        "currency", "availability", "ratevalidfrom", "ratevalidto",
        "bookingdeadline", "materialdeadline",
    ];

    internal static IReadOnlyList<InventoryExtractedRow> Merge(
        IReadOnlyList<InventorySemanticPacket> packets,
        IReadOnlyList<AgentSemanticResult> results)
    {
        if (packets.Count != results.Count) throw Invalid();
        var byHash = packets.ToDictionary(
            packet => packet.InputHash, StringComparer.Ordinal);
        var rows = new List<InventoryExtractedRow>();
        foreach (var result in results)
        {
            if (!byHash.TryGetValue(result.InputHash, out var packet) ||
                packet.Operation != InventorySemanticOperations.SourceTranscription)
                throw Invalid();
            ValidateAccounting(packet, result.Response);
            foreach (var candidate in result.Response.Artifact!.Candidates)
                rows.Add(Project(packet, candidate, rows.Count + 1));
        }
        return rows;
    }

    private static InventoryExtractedRow Project(
        InventorySemanticPacket packet,
        ProposedInventoryCandidate candidate,
        int number)
    {
        var allowed = packet.SourceItems.ToDictionary(
            item => item.Locator, item => item.Content,
            StringComparer.Ordinal);
        var images = packet.Images.Select(image => image.Locator)
            .ToHashSet(StringComparer.Ordinal);
        if ((!allowed.ContainsKey(candidate.SourceLocator) &&
             !images.Contains(candidate.SourceLocator)) ||
            candidate.Fields.Count == 0)
            throw Invalid();
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var locators = new Dictionary<string, string>(StringComparer.Ordinal);
        var confidences = new Dictionary<string, decimal?>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        var transformations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in candidate.Fields)
        {
            var key = InventoryCandidateNormalizer.NormalizeHeader(field.FieldName);
            ValidateField(field, key, allowed, images);
            if (!values.TryAdd(key, field.RawValue.Trim())) throw Invalid();
            locators[key] = field.SourceLocator;
            confidences[key] = field.Confidence;
            evidence[key] = field.EvidenceBasis;
            transformations[key] = field.Transformation;
        }
        if (!values.ContainsKey("name") && !values.ContainsKey("productcode"))
            throw Invalid();
        if (candidate.AmbiguityNotes.Count > 0)
            values["transcriptionambiguity"] =
                string.Join(" | ", candidate.AmbiguityNotes);
        return new InventoryExtractedRow(
            number,
            candidate.SourceLocator,
            values,
            MasterDataCodes.InventoryExtractionMethods.AgentProposal,
            confidences.Values.Where(value => value.HasValue)
                .Select(value => value!.Value).DefaultIfEmpty(0).Min(),
            locators,
            confidences,
            evidence,
            transformations);
    }

    private static void ValidateField(
        ProposedInventoryField field,
        string key,
        Dictionary<string, string> sources,
        HashSet<string> images)
    {
        if (RestrictedFields.Contains(key) ||
            field.NormalizedValue is not null ||
            field.EvidenceBasis !=
                MasterDataCodes.InventoryEvidenceBases.SupplierSupplied ||
            field.Transformation is not (
                MasterDataCodes.InventoryTransformationTypes.Trim or
                MasterDataCodes.InventoryTransformationTypes.DerivedFromSourceContext) ||
            field.Confidence is < 0 or > 1)
            throw Invalid();
        if (images.Contains(field.SourceLocator)) return;
        if (!sources.TryGetValue(field.SourceLocator, out var source) ||
            !source.Contains(field.RawValue.Trim(),
                StringComparison.OrdinalIgnoreCase))
            throw Invalid();
    }

    private static void ValidateAccounting(
        InventorySemanticPacket packet,
        Advertified.Commercial.Infrastructure.Opportunity.AgentRuntimeResponse<
            InventorySemanticExtractionArtifact> response)
    {
        var artifact = response.Artifact ?? throw Invalid();
        var allowed = packet.SourceItems.Select(item => item.Locator)
            .Concat(packet.Images.Select(image => image.Locator))
            .ToHashSet(StringComparer.Ordinal);
        if (artifact.OmittedSourceLocators.Any(locator => !allowed.Contains(locator)))
            throw Invalid();
        var accountedImages = artifact.OmittedSourceLocators
            .Concat(artifact.Candidates.Select(candidate => candidate.SourceLocator))
            .Concat(artifact.Candidates.SelectMany(candidate =>
                candidate.Fields.Select(field => field.SourceLocator)))
            .ToHashSet(StringComparer.Ordinal);
        if (packet.Images.Any(image => !accountedImages.Contains(image.Locator)))
            throw Invalid();
    }

    private static InventorySemanticResultRejectedException Invalid() =>
        new("SOURCE_TRANSCRIPTION_GROUNDING");
}
