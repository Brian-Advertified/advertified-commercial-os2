using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class AudienceIntelligenceValidator
{
    internal static void Validate(
        IReadOnlyList<AudienceDefinitionProposal> audiences,
        IReadOnlyList<string> geographies,
        IReadOnlyList<Guid> evidenceItemIds,
        IReadOnlyList<AudienceEvidenceFact>? structuredEvidence = null,
        IReadOnlyList<ReferenceObservationFact>? referenceEvidence = null,
        IReadOnlyList<string>? clientRequiredAudiences = null)
    {
        ValidateAudienceSet(audiences);
        var allowedGeographies = geographies.ToHashSet(StringComparer.Ordinal);
        var allowedEvidence = evidenceItemIds.ToHashSet();
        var clientRequirements = (clientRequiredAudiences ?? [])
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!clientRequirements.IsSubsetOf(audiences.Select(item => item.Name.Trim())))
            throw new InvalidOperationException("Audience Intelligence omitted an audience required by the approved Brief.");
        var structured = structuredEvidence ?? [];
        var referenceById = (referenceEvidence ?? [])
            .ToDictionary(item => item.ObservationId);

        foreach (var audience in audiences)
        {
            ValidateAudienceText(audience);
            if (HasInvalidCollectionShape(audience)) Reject();

            var referencedObservations = audience.ReferenceObservationIds
                .Where(referenceById.ContainsKey)
                .Select(id => referenceById[id])
                .ToArray();
            if (HasInvalidScopeOrReferences(
                    audience,
                    allowedGeographies,
                    allowedEvidence,
                    referenceById,
                    referencedObservations))
                Reject();

            var hasDirectBriefEvidence = structured.Any(item =>
                item.BriefVersionId.HasValue && string.Equals(
                    item.AudienceName,
                    audience.Name,
                    StringComparison.OrdinalIgnoreCase));
            var hasSupportingEvidence = audience.EvidenceItemIds.Count > 0 ||
                referencedObservations.Length > 0 || hasDirectBriefEvidence;
            var isClientRequirement = clientRequirements.Contains(audience.Name.Trim());
            if (HasInvalidEvidenceSemantics(
                    audience,
                    isClientRequirement,
                    hasSupportingEvidence,
                    referencedObservations.Length > 0) ||
                !HasValidStructuredAudience(audience, structured))
                Reject();
        }
    }

    private static void ValidateAudienceSet(IReadOnlyList<AudienceDefinitionProposal> audiences)
    {
        if (audiences.Count > 20 ||
            audiences.Select(item => item.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != audiences.Count)
        {
            throw new InvalidOperationException(
                "Audience discovery must return one to twenty distinct candidate segments.");
        }
    }

    private static bool HasInvalidCollectionShape(AudienceDefinitionProposal audience) =>
        audience.Geographies is null ||
        audience.Exclusions is null ||
        audience.EvidenceItemIds is null ||
        audience.ReferenceObservationIds is null;

    private static bool HasInvalidScopeOrReferences(
        AudienceDefinitionProposal audience,
        HashSet<string> allowedGeographies,
        HashSet<Guid> allowedEvidence,
        Dictionary<Guid, ReferenceObservationFact> referenceById,
        IReadOnlyList<ReferenceObservationFact> referencedObservations) =>
        (allowedGeographies.Count > 0 && audience.Geographies.Count == 0) ||
        audience.Geographies.Any(item => !allowedGeographies.Contains(item)) ||
        audience.EvidenceItemIds.Any(item => !allowedEvidence.Contains(item)) ||
        audience.ReferenceObservationIds.Any(id => !referenceById.ContainsKey(id)) ||
        referencedObservations.Any(item => item.ActivationPolicy != "AUDIENCE_SEGMENT_SUPPORT");

    private static bool HasInvalidEvidenceSemantics(
        AudienceDefinitionProposal audience,
        bool isClientRequirement,
        bool hasSupportingEvidence,
        bool usesReferenceEvidence)
    {
        var classifiedAsClientRequirement =
            audience.Classification == MasterDataCodes.EvidenceClassifications.ClientRequirement;
        return audience.Confidence is < 0 or > 1 ||
            (!hasSupportingEvidence && audience.Confidence is not null) ||
            isClientRequirement != classifiedAsClientRequirement ||
            !IsAudienceClassification(audience.Classification) ||
            (usesReferenceEvidence &&
                audience.Classification == MasterDataCodes.EvidenceClassifications.Fact);
    }

    private static void ValidateAudienceText(AudienceDefinitionProposal audience)
    {
        RequiredAudienceText(audience.Name, 300);
        RequiredAudienceText(audience.Description, 2_000);
        OptionalAudienceText(audience.NeedState, 1_000);
        OptionalAudienceText(audience.BuyingContext, 1_000);
        OptionalAudienceText(audience.Language, 100);
        OptionalAudienceText(audience.LifeStage, 200);
        OptionalAudienceText(audience.LsmSem, 100);
        OptionalAudienceText(audience.LsmSemTaxonomy, 200);
        OptionalAudienceText(audience.LsmSemTaxonomyVersion, 100);
    }

    private static bool HasValidStructuredAudience(
        AudienceDefinitionProposal audience,
        IReadOnlyList<AudienceEvidenceFact> evidence)
    {
        var hasStructuredValue = audience.Language is not null ||
            audience.LifeStage is not null || audience.LsmSem is not null;
        var evidenceBacked = (audience.EvidenceItemIds.Count > 0 || evidence.Any(item =>
            item.BriefVersionId.HasValue && string.Equals(item.AudienceName,
                audience.Name, StringComparison.OrdinalIgnoreCase))) &&
            audience.Classification is not MasterDataCodes.EvidenceClassifications.Hypothesis;
        var hasLsmSem = !string.IsNullOrWhiteSpace(audience.LsmSem);
        var hasTaxonomy = !string.IsNullOrWhiteSpace(audience.LsmSemTaxonomy) &&
            !string.IsNullOrWhiteSpace(audience.LsmSemTaxonomyVersion);
        var sources = evidence.Where(item =>
            string.Equals(item.AudienceName.Trim(), audience.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            (item.BriefVersionId.HasValue ||
                item.EvidenceItemId.HasValue && audience.EvidenceItemIds.Contains(item.EvidenceItemId.Value)))
            .ToArray();
        return (!hasStructuredValue || evidenceBacked) && hasLsmSem == hasTaxonomy &&
            SupportedValue(audience.Language, sources.Select(item => item.Language)) &&
            SupportedValue(audience.LifeStage, sources.Select(item => item.LifeStage)) &&
            (!hasLsmSem || sources.Any(item => SameValue(audience.LsmSem, item.LsmSem) &&
                SameValue(audience.LsmSemTaxonomy, item.LsmSemTaxonomy) &&
                SameValue(audience.LsmSemTaxonomyVersion, item.LsmSemTaxonomyVersion)));
    }

    private static bool SupportedValue(string? value, IEnumerable<string?> evidence) =>
        value is null || evidence.Any(item => SameValue(value, item));

    private static bool SameValue(string? value, string? evidence) =>
        value is not null && evidence is not null &&
        string.Equals(value.Trim(), evidence.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsAudienceClassification(string value) => value is
        MasterDataCodes.EvidenceClassifications.ClientRequirement or
        MasterDataCodes.EvidenceClassifications.Fact or
        MasterDataCodes.EvidenceClassifications.Inference or
        MasterDataCodes.EvidenceClassifications.Hypothesis;

    private static void Reject() => throw new InvalidOperationException(
        "The audience proposal contains unsupported facts.");

    private static void RequiredAudienceText(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new InvalidOperationException("The audience proposal text is invalid.");
    }

    private static void OptionalAudienceText(string? value, int maximum)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maximum))
            throw new InvalidOperationException("The audience proposal text is invalid.");
    }
}
