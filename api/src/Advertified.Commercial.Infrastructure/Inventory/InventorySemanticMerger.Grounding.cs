using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySemanticMerger
{
    private static void Validate(
        AgentRuntimeResponse<
            InventorySemanticExtractionArtifact> response,
        InventorySemanticPacket packet,
        InventoryCodeSets codes,
        HashSet<string> proposedFields)
    {
        var artifact = response.Artifact ?? throw InvalidGrounding();
        var sources = packet.SourceItems.ToDictionary(
            item => item.Locator,
            item => Comparable(item.Content),
            StringComparer.Ordinal);
        var images = packet.Images
            .Select(image => image.Locator)
            .ToHashSet(StringComparer.Ordinal);
        var existing = packet.ExistingRows.ToDictionary(
            row => row.Locator,
            row => Comparable(string.Join(
                '\n', row.Values.Values)),
            StringComparer.Ordinal);
        var allowedOmissions = sources.Keys
            .Concat(images)
            .ToHashSet(StringComparer.Ordinal);
        if (artifact.OmittedSourceLocators.Any(
                locator => !allowedOmissions.Contains(locator)))
        {
            throw InvalidGrounding();
        }
        foreach (var candidate in artifact.Candidates)
        {
            ValidateCandidate(
                candidate,
                sources,
                images,
                existing,
                codes,
                proposedFields);
        }
        ValidateImageAccounting(artifact, images);
    }

    private static void ValidateCandidate(
        ProposedInventoryCandidate candidate,
        Dictionary<string, string> sources,
        HashSet<string> images,
        Dictionary<string, string> existing,
        InventoryCodeSets codes,
        HashSet<string> proposedFields)
    {
        if (!existing.ContainsKey(candidate.SourceLocator) ||
            candidate.Fields.Count == 0 ||
            candidate.Fields
                .GroupBy(field => field.FieldName)
                .Any(group => group.Count() > 1))
        {
            throw InvalidGrounding();
        }
        foreach (var field in candidate.Fields)
        {
            var proposalKey = candidate.SourceLocator +
                "\0" + field.FieldName;
            if (!proposedFields.Add(proposalKey))
                throw InvalidGrounding();
            ValidateField(
                field,
                sources,
                images,
                existing,
                codes);
        }
    }

    private static void ValidateField(
        ProposedInventoryField field,
        Dictionary<string, string> sources,
        HashSet<string> images,
        Dictionary<string, string> existing,
        InventoryCodeSets codes)
    {
        if (!EnrichmentFields.Contains(field.FieldName) ||
            field.EvidenceBasis !=
                MasterDataCodes.InventoryEvidenceBases.DerivedPolicy ||
            string.IsNullOrWhiteSpace(field.NormalizedValue) ||
            !EnrichmentTransformations.Contains(
                field.Transformation) ||
            field.Confidence is < 0 or > 1 ||
            !IsSourceGrounded(
                field, sources, images, existing))
        {
            throw InvalidGrounding();
        }
        ValidateCode(field, codes);
    }

    private static bool IsSourceGrounded(
        ProposedInventoryField field,
        Dictionary<string, string> sources,
        HashSet<string> images,
        IReadOnlyDictionary<string, string> existing)
    {
        if (images.Contains(field.SourceLocator))
            return true;
        var source = sources.GetValueOrDefault(
            field.SourceLocator) ??
            existing.GetValueOrDefault(field.SourceLocator);
        return source is not null &&
            source.Contains(
                Comparable(field.RawValue),
                StringComparison.Ordinal);
    }

    private static void ValidateImageAccounting(
        InventorySemanticExtractionArtifact artifact,
        HashSet<string> images)
    {
        if (images.Count == 0)
            return;
        var accounted = artifact.OmittedSourceLocators
            .Concat(artifact.Candidates.Select(
                candidate => candidate.SourceLocator))
            .Concat(artifact.Candidates.SelectMany(
                candidate => candidate.Fields.Select(
                    field => field.SourceLocator)))
            .ToHashSet(StringComparer.Ordinal);
        if (!images.IsSubsetOf(accounted))
            throw InvalidGrounding();
    }
}
