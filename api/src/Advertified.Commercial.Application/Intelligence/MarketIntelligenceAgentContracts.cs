using Advertified.Commercial.Application.Opportunity;

namespace Advertified.Commercial.Application.Intelligence;

public sealed record MarketIntelligenceInput(
    CommercialProblemInput Problem,
    IReadOnlyList<AgentEvidenceInput> ApprovedEvidence);

public sealed record MarketFindingProposal(
    string Title,
    string Finding,
    string CommercialImplication,
    string Classification,
    IReadOnlyList<Guid> EvidenceItemIds,
    decimal? Confidence);

public sealed record MarketOpportunityProposal(
    string Title,
    string Rationale,
    IReadOnlyList<string> RequiredEvidence,
    string Priority);

public sealed record MarketIntelligenceAgentProposal(
    string CategorySituation,
    IReadOnlyList<MarketFindingProposal> Findings,
    IReadOnlyList<MarketOpportunityProposal> Opportunities,
    IReadOnlyList<string> StrategicImplications,
    IReadOnlyList<string> EvidenceGaps,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    IntelligenceInvocationUsage Usage);

public interface IMarketIntelligenceAgentClient
{
    Task<MarketIntelligenceAgentProposal> AnalyseAsync(
        MarketIntelligenceInput input,
        CancellationToken cancellationToken);
}

public interface IMarketIntelligenceService
{
    Task<IntelligenceArtifactView> AnalyseBriefAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);

    Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);
}
