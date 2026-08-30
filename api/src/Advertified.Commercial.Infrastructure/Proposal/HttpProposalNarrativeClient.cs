using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed class HttpProposalNarrativeClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options,
    ProposalPolicy proposalPolicy) : IProposalNarrativeClient
{
    private const string BriefVersionResourceType = "BriefVersion";

    public async Task<ProposalNarrative> CreateAsync(
        ProposalNarrativeInput input,
        CancellationToken cancellationToken)
    {
        EnsureInput(input);
        var agentCode = MasterDataCodes.AgentTypes.ProposalNarrative;
        var payload = new ProposalRequest(
            AgentRuntimeHttpSupport.CreateInvocation(
                input.TenantId,
                input.ActorId,
                input.RunId,
                input.RunId,
                input.CorrelationId,
                agentCode,
                ResourceReferences(input),
                input.EvidenceItemIds),
            new ProposalContext(
                input.BriefVersionId,
                input.BriefObjective,
                input.Options.Select(item => new ProposalOption(
                    item.PlanVersionId,
                    item.PlanVersion,
                    item.Label,
                    item.Outcome,
                    item.BudgetMinor,
                    item.Currency,
                    item.Channels)).ToArray()));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<ProposalArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.EvidenceItemIds,
            cancellationToken);
        if (output.Status != MasterDataCodes.LifecycleStatuses.Completed)
        {
            throw new InvalidOperationException(
                "The proposal narrative agent did not complete its proposal.");
        }
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The proposal narrative artifact is unavailable.");
        ValidateNarrative(artifact.ExecutiveSummary, input);
        return new ProposalNarrative(
            artifact.ExecutiveSummary,
            output.Usage.IncrementalCostMinor);
    }

    private void EnsureInput(ProposalNarrativeInput input)
    {
        if (input.Options.Count < proposalPolicy.MinimumOptions ||
            input.Options.Count > proposalPolicy.MaximumOptions)
        {
            throw new ArgumentException("The proposal choice count is outside the account policy.");
        }
    }

    private static AgentResourceReference[] ResourceReferences(
        ProposalNarrativeInput input) =>
        [
            new(BriefVersionResourceType, input.BriefVersionId, input.BriefVersion),
            .. input.Options.Select(option => new AgentResourceReference(
                "MediaPlanVersion", option.PlanVersionId, option.PlanVersion)),
        ];

    private static void ValidateNarrative(
        string narrative,
        ProposalNarrativeInput input)
    {
        if (string.IsNullOrWhiteSpace(narrative) || narrative.Length > 5_000 ||
            !narrative.Contains(input.BriefObjective, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The proposal narrative does not preserve the approved objective.");
        }
        if (input.Options.Any(option =>
                !narrative.Contains(option.Label, StringComparison.Ordinal) ||
                !narrative.Contains(option.Outcome, StringComparison.OrdinalIgnoreCase) ||
                !narrative.Contains(
                    ProposalMoneyFormatter.Format(option.BudgetMinor, option.Currency),
                    StringComparison.Ordinal) ||
                option.Channels.Any(channel =>
                    !narrative.Contains(channel, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "The proposal narrative does not preserve the approved option facts.");
        }
    }

    private sealed record ProposalRequest(
        AgentInvocationRequest Invocation,
        ProposalContext Proposal);

    private sealed record ProposalContext(
        Guid BriefVersionId,
        string BriefObjective,
        IReadOnlyList<ProposalOption> Options);

    private sealed record ProposalOption(
        Guid PlanVersionId,
        int PlanVersion,
        string Label,
        string Outcome,
        long BudgetMinor,
        string Currency,
        IReadOnlyList<string> Channels);

    private sealed class ProposalArtifact
    {
        public required string ExecutiveSummary { get; init; }
    }
}
