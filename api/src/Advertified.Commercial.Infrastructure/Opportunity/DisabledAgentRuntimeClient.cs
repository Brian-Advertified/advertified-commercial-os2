using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Application.Proposal;

namespace Advertified.Commercial.Infrastructure.Opportunity;

// One unavailable boundary for the shared runtime, with no fixture or network dependency.
public sealed class DisabledAgentRuntimeClient : IOpportunityAgentClient, IPlanningAgentClient,
    IProposalNarrativeClient, IMeasurementAgentClient
{
    public Task<OpportunityAgentOutput> InvokeAsync(
        OpportunityAgentInput input, CancellationToken cancellationToken) => Unavailable<OpportunityAgentOutput>(cancellationToken);

    public Task<AudienceAgentProposal> ProposeAudiencesAsync(
        PlanningBriefInput input, CancellationToken cancellationToken) => Unavailable<AudienceAgentProposal>(cancellationToken);

    public Task<MediaPlanningAgentProposal> ProposeMediaMixAsync(
        MediaPlanningInput input, CancellationToken cancellationToken) => Unavailable<MediaPlanningAgentProposal>(cancellationToken);

    public Task<InventoryIntelligenceAgentProposal> InterpretInventoryAsync(
        InventoryIntelligenceInput input, CancellationToken cancellationToken) => Unavailable<InventoryIntelligenceAgentProposal>(cancellationToken);

    public Task<ProposalNarrative> CreateAsync(
        ProposalNarrativeInput input, CancellationToken cancellationToken) => Unavailable<ProposalNarrative>(cancellationToken);

    public Task<MeasurementAgentProposal> InterpretAsync(
        MeasurementAgentInput input, CancellationToken cancellationToken) => Unavailable<MeasurementAgentProposal>(cancellationToken);

    private static Task<T> Unavailable<T>(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new AgentRuntimeUnavailableException();
    }
}
