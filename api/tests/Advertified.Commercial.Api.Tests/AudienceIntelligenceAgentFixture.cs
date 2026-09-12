using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Tests;

internal sealed class AudienceIntelligenceAgentFixture : IAudienceIntelligenceAgentClient
{
    public Task<AudienceAgentProposal> ProposeAudiencesAsync(
        AudienceIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var approvedEvidence = input.Problem.EvidenceItemIds.ToHashSet();
        var audiences = input.Problem.Audiences
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => BuildAudience(input, name, approvedEvidence))
            .ToArray();
        var targets = audiences.Where(item => item.IsTarget).Select(item => item.Name).ToArray();
        var targetingRationale = targets.Length == 0
            ? null
            : $"Prioritise {string.Join(", ", targets)} for the stated objective within {string.Join(", ", input.Problem.Geographies)}. Unstated motivations and behaviours remain unestablished.";
        return Task.FromResult(new AudienceAgentProposal(
            audiences,
            targetingRationale,
            null,
            [
                "Audience need state is not established unless supported by audience-bound evidence.",
                "Buying context is not established unless supported by audience-bound evidence.",
                "Positioning requires evidence-backed product or brand claims.",
            ],
            "The deterministic fixture preserves explicit Brief audiences and only retains audience-bound evidence.",
            new IntelligenceInvocationUsage(
                MasterDataCodes.AgentTypes.AudienceIntelligence,
                "deterministic",
                "fixture-v1",
                0,
                "FIXTURE",
                null,
                0,
                0,
                0)));
    }

    private static AudienceDefinitionProposal BuildAudience(
        AudienceIntelligenceInput input,
        string name,
        HashSet<Guid> approvedEvidence)
    {
        var facts = input.Evidence
            .Where(item => string.Equals(item.AudienceName, name, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var evidenceIds = facts
            .Where(item => item.EvidenceItemId.HasValue && approvedEvidence.Contains(item.EvidenceItemId.Value))
            .Select(item => item.EvidenceItemId!.Value)
            .Distinct()
            .ToArray();
        var needState = Single(facts.Select(item => item.NeedState));
        var buyingContext = Single(facts.Select(item => item.BuyingContext));
        var language = Single(facts.Select(item => item.Language));
        var lifeStage = Single(facts.Select(item => item.LifeStage));
        var lsmSem = Single(facts.Select(item => item.LsmSem));
        var taxonomy = Single(facts.Select(item => item.LsmSemTaxonomy));
        var taxonomyVersion = Single(facts.Select(item => item.LsmSemTaxonomyVersion));
        if (lsmSem is null || taxonomy is null || taxonomyVersion is null)
        {
            lsmSem = null;
            taxonomy = null;
            taxonomyVersion = null;
        }

        return new AudienceDefinitionProposal(
            name,
            $"Brief-supplied audience: {name}. Additional motivations, buying intent, affiliations and behaviours are not established unless separately supported by approved evidence.",
            needState,
            buyingContext,
            input.Problem.Geographies,
            language,
            lifeStage,
            lsmSem,
            taxonomy,
            taxonomyVersion,
            MasterDataCodes.EvidenceClassifications.ClientRequirement,
            [],
            evidenceIds,
            [],
            null,
            true);
    }

    private static string? Single(IEnumerable<string?> values)
    {
        var supplied = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return supplied.Length == 1 ? supplied[0] : null;
    }
}
