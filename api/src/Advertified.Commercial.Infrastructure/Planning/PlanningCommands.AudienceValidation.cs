using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Planning;

internal static class PlanningAudienceProposalValidator
{
    internal static void Validate(
        IReadOnlyList<AudienceDefinitionProposal> audiences,
        IReadOnlyList<string> geographies,
        IReadOnlyList<Guid> evidenceItemIds)
    {
        var allowedGeographies = geographies.ToHashSet(StringComparer.Ordinal);
        var allowedEvidence = evidenceItemIds.ToHashSet();
        foreach (var audience in audiences)
        {
            ValidateAudienceText(audience);
            if (audience.Geographies is null || audience.Exclusions is null ||
                audience.EvidenceItemIds is null ||
                audience.Geographies.Any(item => !allowedGeographies.Contains(item)) ||
                audience.EvidenceItemIds.Any(item => !allowedEvidence.Contains(item)) ||
                audience.Confidence is < 0 or > 1 ||
                !IsAudienceClassification(audience.Classification) ||
                !HasValidStructuredAudience(audience))
            {
                throw new InvalidOperationException(
                    "The audience proposal contains unsupported facts.");
            }
        }
    }

    private static void ValidateAudienceText(AudienceDefinitionProposal audience)
    {
        RequiredAudienceText(audience.Name, 300);
        RequiredAudienceText(audience.Description, 2_000);
        RequiredAudienceText(audience.NeedState, 1_000);
        RequiredAudienceText(audience.BuyingContext, 1_000);
        OptionalAudienceText(audience.Language, 100);
        OptionalAudienceText(audience.LifeStage, 200);
        OptionalAudienceText(audience.LsmSem, 100);
        OptionalAudienceText(audience.LsmSemTaxonomy, 200);
        OptionalAudienceText(audience.LsmSemTaxonomyVersion, 100);
    }

    private static bool HasValidStructuredAudience(AudienceDefinitionProposal audience)
    {
        var hasStructuredValue = audience.Language is not null ||
            audience.LifeStage is not null || audience.LsmSem is not null;
        var evidenceBacked = audience.EvidenceItemIds.Count > 0 &&
            audience.Classification is not MasterDataCodes.EvidenceClassifications.Hypothesis;
        var hasLsmSem = !string.IsNullOrWhiteSpace(audience.LsmSem);
        var hasTaxonomy = !string.IsNullOrWhiteSpace(audience.LsmSemTaxonomy) &&
            !string.IsNullOrWhiteSpace(audience.LsmSemTaxonomyVersion);
        return (!hasStructuredValue || evidenceBacked) && hasLsmSem == hasTaxonomy;
    }

    private static bool IsAudienceClassification(string value) => value is
        MasterDataCodes.EvidenceClassifications.Fact or
        MasterDataCodes.EvidenceClassifications.Inference or
        MasterDataCodes.EvidenceClassifications.Hypothesis;

    private static void RequiredAudienceText(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
        {
            throw new InvalidOperationException("The audience proposal text is invalid.");
        }
    }

    private static void OptionalAudienceText(string? value, int maximum)
    {
        if (value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > maximum))
        {
            throw new InvalidOperationException("The audience proposal text is invalid.");
        }
    }
}
