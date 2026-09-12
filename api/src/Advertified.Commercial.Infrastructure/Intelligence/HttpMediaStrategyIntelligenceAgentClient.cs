using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Intelligence;

public sealed class HttpMediaStrategyIntelligenceAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IMediaStrategyIntelligenceAgentClient
{
    private const string Operation = "MEDIA_STRATEGY_ANALYSIS";

    public async Task<MediaStrategyIntelligenceProposal> AnalyseAsync(
        MediaStrategyIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.MediaStrategy;
        var resources = new List<AgentResourceReference>
        {
            new("BriefVersion", input.Problem.BriefVersionId, input.Problem.BriefVersion),
            new("IntelligenceArtifact", input.AudienceArtifactId, input.AudienceArtifactVersion),
        };
        if (input.LocationArtifactId.HasValue && input.LocationArtifactVersion.HasValue)
            resources.Add(new AgentResourceReference(
                "IntelligenceArtifact", input.LocationArtifactId.Value, input.LocationArtifactVersion.Value));

        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            input.Problem.TenantId,
            input.Problem.ActorId,
            input.Problem.RunId,
            Guid.NewGuid(),
            input.Problem.CorrelationId,
            agentCode,
            resources,
            input.Problem.EvidenceItemIds,
            options.Value,
            modelOperation: Operation);
        var payload = new MediaStrategyRequest(
            Operation,
            invocation,
            new MediaStrategyContext(
                input.Problem.BriefVersionId,
                input.AudienceArtifactId,
                input.AudienceArtifactVersion,
                input.LocationArtifactId,
                input.LocationArtifactVersion,
                input.Problem.ClientName,
                input.Problem.BusinessProblem,
                input.Problem.Objective,
                input.Problem.Geographies,
                input.Problem.MediaRequirements,
                input.Problem.Constraints,
                input.Problem.SuccessMeasures,
                input.Problem.BudgetMinor,
                input.Problem.Currency,
                input.AvailableChannels,
                input.TargetAudiences,
                input.LocationOpportunities,
                input.AudienceUnknowns,
                input.LocationEvidenceGaps));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<MediaStrategyArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.Problem.EvidenceItemIds,
            cancellationToken,
            Operation);
        if (output.Status != MasterDataCodes.LifecycleStatuses.Completed)
            throw new InvalidOperationException("Media Strategy Intelligence did not complete.");
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Media Strategy Intelligence artifact is unavailable.");
        Validate(artifact, input);
        return new MediaStrategyIntelligenceProposal(
            artifact.Summary,
            artifact.ChannelRecommendations.Select(item => new MediaChannelRecommendationProposal(
                item.Channel,
                item.Role,
                item.Rationale,
                item.ObjectiveContribution,
                item.GeographyRole,
                item.Classification,
                item.BudgetGuidancePercent,
                item.TradeOffs,
                item.EvidenceGaps)).ToArray(),
            artifact.StrategicPrinciples,
            artifact.ExcludedChannels,
            artifact.EvidenceGaps,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Assumptions.Select(item => item.Value).ToArray(),
            output.Rationale,
            IntelligenceUsage.FromRuntime(Operation, output.Usage));
    }

    private static void Validate(MediaStrategyArtifact artifact, MediaStrategyIntelligenceInput input)
    {
        if (string.IsNullOrWhiteSpace(artifact.Summary) ||
            artifact.ChannelRecommendations is null || artifact.StrategicPrinciples is null ||
            artifact.ExcludedChannels is null || artifact.EvidenceGaps is null)
            throw new InvalidOperationException("The Media Strategy Intelligence artifact is incomplete.");

        var allowed = input.AvailableChannels.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in artifact.ChannelRecommendations)
        {
            if (item is null || !allowed.Contains(item.Channel) || !seen.Add(item.Channel) ||
                string.IsNullOrWhiteSpace(item.Role) || string.IsNullOrWhiteSpace(item.Rationale) ||
                string.IsNullOrWhiteSpace(item.ObjectiveContribution) ||
                item.Classification is not (
                    MasterDataCodes.EvidenceClassifications.Inference or
                    MasterDataCodes.EvidenceClassifications.Hypothesis) ||
                item.BudgetGuidancePercent is < 0 or > 100)
                throw new InvalidOperationException("Media Strategy Intelligence returned an unsupported channel recommendation.");
        }
        if (artifact.ExcludedChannels.Any(item => !allowed.Contains(item)))
            throw new InvalidOperationException("Media Strategy Intelligence returned an unsupported channel exclusion.");
        if (!input.Problem.BudgetMinor.HasValue &&
            artifact.ChannelRecommendations.Any(item => item.BudgetGuidancePercent.HasValue))
            throw new InvalidOperationException("Budgetless Media Strategy Intelligence may not invent a budget split.");
    }

    private sealed record MediaStrategyRequest(
        string Operation,
        AgentInvocationRequest Invocation,
        MediaStrategyContext MediaStrategy);

    private sealed record MediaStrategyContext(
        Guid BriefVersionId,
        Guid AudienceArtifactId,
        long AudienceArtifactVersion,
        Guid? LocationArtifactId,
        long? LocationArtifactVersion,
        string ClientName,
        string BusinessProblem,
        string Objective,
        IReadOnlyList<string> Geographies,
        IReadOnlyList<string> MediaRequirements,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<string> SuccessMeasures,
        long? BudgetMinor,
        string? Currency,
        IReadOnlyList<string> AvailableChannels,
        IReadOnlyList<MediaStrategyAudienceContext> TargetAudiences,
        IReadOnlyList<MediaStrategyLocationContext> LocationOpportunities,
        IReadOnlyList<string> AudienceUnknowns,
        IReadOnlyList<string> LocationEvidenceGaps);

    private sealed class MediaStrategyArtifact
    {
        public required string Summary { get; init; }
        public required MediaChannelRecommendation[] ChannelRecommendations { get; init; }
        public required string[] StrategicPrinciples { get; init; }
        public required string[] ExcludedChannels { get; init; }
        public required string[] EvidenceGaps { get; init; }
    }

    private sealed class MediaChannelRecommendation
    {
        public required string Channel { get; init; }
        public required string Role { get; init; }
        public required string Rationale { get; init; }
        public required string ObjectiveContribution { get; init; }
        public string? GeographyRole { get; init; }
        public required string Classification { get; init; }
        public decimal? BudgetGuidancePercent { get; init; }
        public required string[] TradeOffs { get; init; }
        public required string[] EvidenceGaps { get; init; }
    }
}
