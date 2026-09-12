namespace Advertified.Commercial.Application.Intelligence;

public sealed record MediaStrategyAudienceContext(
    Guid SegmentId,
    string Name,
    string Description,
    IReadOnlyList<string> Geographies,
    string Classification);

public sealed record MediaStrategyLocationContext(
    string Name,
    string Geography,
    string Rationale,
    string Classification);

public sealed record MediaStrategyIntelligenceInput(
    CommercialProblemInput Problem,
    Guid AudienceArtifactId,
    long AudienceArtifactVersion,
    Guid? LocationArtifactId,
    long? LocationArtifactVersion,
    IReadOnlyList<MediaStrategyAudienceContext> TargetAudiences,
    IReadOnlyList<MediaStrategyLocationContext> LocationOpportunities,
    IReadOnlyList<string> AudienceUnknowns,
    IReadOnlyList<string> LocationEvidenceGaps,
    IReadOnlyList<string> AvailableChannels);

public sealed record MediaChannelRecommendationProposal(
    string Channel,
    string Role,
    string Rationale,
    string ObjectiveContribution,
    string? GeographyRole,
    string Classification,
    decimal? BudgetGuidancePercent,
    IReadOnlyList<string> TradeOffs,
    IReadOnlyList<string> EvidenceGaps);

public sealed record MediaStrategyIntelligenceArtifact(
    string Summary,
    IReadOnlyList<MediaChannelRecommendationProposal> ChannelRecommendations,
    IReadOnlyList<string> StrategicPrinciples,
    IReadOnlyList<string> ExcludedChannels,
    IReadOnlyList<string> EvidenceGaps);

public sealed record MediaStrategyIntelligenceProposal(
    string Summary,
    IReadOnlyList<MediaChannelRecommendationProposal> ChannelRecommendations,
    IReadOnlyList<string> StrategicPrinciples,
    IReadOnlyList<string> ExcludedChannels,
    IReadOnlyList<string> EvidenceGaps,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<string> Assumptions,
    string Rationale,
    IntelligenceInvocationUsage Usage);

public interface IMediaStrategyIntelligenceAgentClient
{
    Task<MediaStrategyIntelligenceProposal> AnalyseAsync(
        MediaStrategyIntelligenceInput input,
        CancellationToken cancellationToken);
}

public interface IMediaStrategyIntelligenceService
{
    Task<IntelligenceArtifactView> AnalyseBriefAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);

    Task<IntelligenceArtifactView> ApproveAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        Guid artifactId,
        long expectedVersion,
        CancellationToken cancellationToken);

    Task<IntelligenceArtifactView?> GetLatestAsync(
        Guid actorId,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);
}
