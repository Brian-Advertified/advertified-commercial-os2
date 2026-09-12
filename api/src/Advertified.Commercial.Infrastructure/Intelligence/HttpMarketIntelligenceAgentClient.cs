using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Intelligence;

public sealed class HttpMarketIntelligenceAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IMarketIntelligenceAgentClient
{
    private const string BriefVersionResourceType = "BriefVersion";

    public async Task<MarketIntelligenceAgentProposal> AnalyseAsync(
        MarketIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        MarketIntelligenceValidator.ValidateInput(input);
        var agentCode = MasterDataCodes.AgentTypes.MarketIntelligence;
        var output = await AgentRuntimeHttpSupport.InvokeAsync<MarketArtifact>(
            httpClient, options.Value, agentCode, CreatePayload(input, agentCode),
            input.Problem.EvidenceItemIds, cancellationToken);
        if (output.Status != MasterDataCodes.LifecycleStatuses.Completed)
            throw new InvalidOperationException("Market Intelligence did not complete its proposal.");
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Market Intelligence artifact is unavailable.");
        var proposal = new MarketIntelligenceAgentProposal(
            artifact.CategorySituation, artifact.Findings, artifact.Opportunities,
            artifact.StrategicImplications, artifact.EvidenceGaps,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Assumptions.Select(item => item.Value).ToArray(),
            output.Rationale, IntelligenceUsage.FromRuntime(agentCode, output.Usage));
        MarketIntelligenceValidator.Validate(proposal, input);
        return proposal;
    }

    private MarketRequest CreatePayload(MarketIntelligenceInput input, string agentCode)
    {
        var problem = input.Problem;
        return new MarketRequest(
            AgentRuntimeHttpSupport.CreateInvocation(
                problem.TenantId,
                problem.ActorId,
                problem.RunId,
                problem.RunId,
                problem.CorrelationId,
                agentCode,
                BriefVersionResourceType,
                problem.BriefVersionId,
                problem.BriefVersion,
                problem.EvidenceItemIds,
                options.Value),
            new MarketContext(
                problem.BriefVersionId,
                problem.ClientName,
                problem.BusinessProblem,
                problem.Objective,
                problem.Audiences,
                problem.Geographies,
                problem.MediaRequirements,
                problem.Constraints,
                problem.Conflicts,
                problem.SuccessMeasures,
                problem.BudgetMinor,
                problem.Currency),
            input.ApprovedEvidence);
    }

    private sealed record MarketRequest(
        AgentInvocationRequest Invocation,
        MarketContext Market,
        IReadOnlyList<AgentEvidenceInput> ApprovedEvidence);

    private sealed record MarketContext(
        Guid BriefVersionId,
        string ClientName,
        string BusinessProblem,
        string Objective,
        IReadOnlyList<string> Audiences,
        IReadOnlyList<string> Geographies,
        IReadOnlyList<string> MediaRequirements,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<CommercialProblemConflictInput> Conflicts,
        IReadOnlyList<string> SuccessMeasures,
        long? BudgetMinor,
        string? Currency);

    private sealed class MarketArtifact
    {
        public required string CategorySituation { get; init; }
        public required MarketFindingProposal[] Findings { get; init; }
        public required MarketOpportunityProposal[] Opportunities { get; init; }
        public required string[] StrategicImplications { get; init; }
        public required string[] EvidenceGaps { get; init; }
    }
}
