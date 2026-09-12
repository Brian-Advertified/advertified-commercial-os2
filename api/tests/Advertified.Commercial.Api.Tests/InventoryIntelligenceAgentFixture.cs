using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Tests;

internal sealed class InventoryIntelligenceAgentFixture : IInventoryIntelligenceAgentClient
{
    public Task<InventoryIntelligenceProposal> InterpretShortlistAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var interpretations = input.Candidates.Select(candidate =>
            new InventoryCandidateInterpretation(
                candidate.CandidateId,
                candidate.IsEligible
                    ? $"{candidate.Name} is eligible under the supplied deterministic planning facts; human selection remains separate."
                    : $"{candidate.Name} is excluded by the supplied deterministic eligibility result: {candidate.RejectionDetail}",
                MasterDataCodes.EvidenceClassifications.AiRecommendation)).ToArray();
        return Task.FromResult(new InventoryIntelligenceProposal(
            interpretations,
            [],
            [],
            "The fixture explains supplied deterministic shortlist facts without changing eligibility, scores or selection.",
            new IntelligenceInvocationUsage(
                MasterDataCodes.AgentTypes.InventoryIntelligence,
                "deterministic",
                "fixture-v1",
                0,
                "FIXTURE",
                null,
                0,
                0,
                0)));
    }
}
