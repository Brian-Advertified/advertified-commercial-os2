using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Intelligence;

internal static class MarketIntelligenceValidator
{
    // Wire-contract bound, not a business ranking or evidence-quality threshold.
    internal const int MaximumEvidenceSnapshots = 100;

    internal static void ValidateInput(MarketIntelligenceInput input)
    {
        var allowed = input.Problem.EvidenceItemIds.ToHashSet();
        var evidence = input.ApprovedEvidence;
        if (evidence is null || evidence.Count > MaximumEvidenceSnapshots ||
            evidence.Any(item => item is null) ||
            evidence.Select(item => item.Id).Distinct().Count() != evidence.Count)
            throw new InvalidOperationException("Market Intelligence evidence snapshots are invalid.");
        foreach (var item in evidence)
        {
            if (!allowed.Contains(item.Id) || item.StructuredValue.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Market Intelligence evidence is outside the approved input.");
            RequireText(item.ClaimType, 100);
            RequireText(item.Excerpt, 2_000);
        }
    }

    internal static void Validate(MarketIntelligenceAgentProposal proposal, MarketIntelligenceInput input)
    {
        ValidateInput(input);
        RequireText(proposal.CategorySituation, 4_000);
        if (proposal.Findings is null || proposal.Findings.Count > 20 ||
            proposal.Opportunities is null || proposal.Opportunities.Count > 10 ||
            proposal.StrategicImplications is null || proposal.StrategicImplications.Count is < 1 or > 12 ||
            proposal.EvidenceGaps is null || proposal.Unknowns is null || proposal.Assumptions is null)
            throw new InvalidOperationException("The Market Intelligence artifact is incomplete.");
        RequireTexts(proposal.StrategicImplications, 1_000);
        RequireTexts(proposal.EvidenceGaps, 1_000);
        if (proposal.Findings.Count == 0 && proposal.Opportunities.Count == 0 && proposal.EvidenceGaps.Count == 0)
            throw new InvalidOperationException("A market result without conclusions must explain its evidence gap.");
        var supplied = input.ApprovedEvidence.Select(item => item.Id).ToHashSet();
        foreach (var finding in proposal.Findings) ValidateFinding(finding, supplied);
        foreach (var opportunity in proposal.Opportunities)
        {
            if (opportunity is null || opportunity.RequiredEvidence is null)
                throw new InvalidOperationException("The Market Intelligence opportunity is incomplete.");
            RequireText(opportunity.Title, 300);
            RequireText(opportunity.Rationale, 2_000);
            RequireText(opportunity.Priority, 100);
            RequireTexts(opportunity.RequiredEvidence, 500);
            if (supplied.Count == 0 && opportunity.RequiredEvidence.Count == 0)
                throw new InvalidOperationException("An unevidenced market opportunity must state required evidence.");
        }
    }

    private static void ValidateFinding(MarketFindingProposal finding, HashSet<Guid> supplied)
    {
        if (finding is null || finding.EvidenceItemIds is null ||
            finding.EvidenceItemIds.Any(item => !supplied.Contains(item)) ||
            finding.Confidence is < 0 or > 1 ||
            finding.Classification is not (
                MasterDataCodes.EvidenceClassifications.Fact or
                MasterDataCodes.EvidenceClassifications.Inference or
                MasterDataCodes.EvidenceClassifications.Hypothesis))
            throw new InvalidOperationException("Market Intelligence returned an unsupported finding.");
        RequireText(finding.Title, 300);
        RequireText(finding.Finding, 2_000);
        RequireText(finding.CommercialImplication, 2_000);
        if (finding.EvidenceItemIds.Count == 0 &&
            (finding.Classification != MasterDataCodes.EvidenceClassifications.Hypothesis ||
             finding.Confidence is not null))
            throw new InvalidOperationException("Unevidenced market findings must remain hypotheses without numeric confidence.");
    }

    private static void RequireTexts(IReadOnlyList<string> values, int maximum)
    {
        foreach (var value in values) RequireText(value, maximum);
    }

    private static void RequireText(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum)
            throw new InvalidOperationException("The Market Intelligence artifact contains invalid text.");
    }
}
