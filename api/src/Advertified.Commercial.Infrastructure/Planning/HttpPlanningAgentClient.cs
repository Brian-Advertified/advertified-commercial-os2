using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class HttpPlanningAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IPlanningAgentClient
{
    private const string BriefVersionResourceType = "BriefVersion";
    private HttpClient RuntimeHttpClient => httpClient;
    private AgentRuntimeOptions RuntimeSettings => options.Value;

    public async Task<AudienceAgentProposal> ProposeAudiencesAsync(
        PlanningBriefInput input,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.Audience;
        var payload = new AudienceRequest(
            CreateInvocation(input, agentCode),
            new AudienceContext(
                input.BriefVersionId,
                input.Objective,
                input.Audiences,
                input.Geographies));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<AudienceArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.EvidenceItemIds,
            cancellationToken);
        EnsureCompleted(output.Status);
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The audience artifact is unavailable.");
        ValidateAudienceArtifact(artifact, input, output.EvidenceBindings);
        return new AudienceAgentProposal(
            artifact.Audiences.Select(ToProposal).ToArray(),
            artifact.TargetingRationale,
            artifact.PositioningStatement,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Rationale,
            output.Usage.Provider,
            output.Usage.Model,
            output.Usage.IncrementalCostMinor,
            output.Usage.ProviderRequestId);
    }

    public async Task<MediaPlanningAgentProposal> ProposeMediaMixAsync(
        MediaPlanningInput input,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.MediaPlanning;
        var brief = input.Brief;
        var payload = new MediaPlanningRequest(
            CreateInvocation(brief, agentCode),
            new MediaPlanningContext(
                brief.BriefVersionId,
                brief.Objective,
                brief.Audiences,
                brief.Geographies,
                input.BudgetMinor,
                input.Currency,
                input.AvailableChannels));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<MediaMixArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            brief.EvidenceItemIds,
            cancellationToken);
        EnsureCompleted(output.Status);
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The media-mix artifact is unavailable.");
        ValidateMediaMixArtifact(artifact, input);
        return new MediaPlanningAgentProposal(
            artifact.Allocations.Select(item => new MediaAllocationProposal(
                item.Channel,
                item.BudgetMinor,
                item.Role,
                Array.Empty<MediaRunningPeriodInput>())).ToArray(),
            output.Unknowns.Select(item => item.Question).ToArray(),
            artifact.Assumptions.Concat(output.Assumptions.Select(item => item.Value)).ToArray(),
            output.Rationale,
            output.Usage.Provider,
            output.Usage.Model,
            output.Usage.IncrementalCostMinor,
            output.Usage.ProviderRequestId);
    }

    private AgentInvocationRequest CreateInvocation(
        PlanningBriefInput input,
        string agentCode) => AgentRuntimeHttpSupport.CreateInvocation(
            input.TenantId,
            input.ActorId,
            input.RunId,
            input.RunId,
            input.CorrelationId,
            agentCode,
            BriefVersionResourceType,
            input.BriefVersionId,
            input.BriefVersion,
            input.EvidenceItemIds,
            RuntimeSettings);

    private static AudienceDefinitionProposal ToProposal(AudienceDefinition item) => new(
        item.Name,
        item.Description,
        item.NeedState,
        item.BuyingContext,
        item.Geographies,
        item.Language,
        item.LifeStage,
        item.LsmSem,
        item.Classification,
        item.Exclusions,
        item.EvidenceItemIds,
        item.Confidence,
        item.IsTarget);

    private static void ValidateAudienceArtifact(
        AudienceArtifact artifact,
        PlanningBriefInput input,
        IReadOnlyList<AgentEvidenceBinding> bindings)
    {
        if (artifact.Audiences is null || artifact.Audiences.Length == 0 ||
            string.IsNullOrWhiteSpace(artifact.TargetingRationale) ||
            string.IsNullOrWhiteSpace(artifact.PositioningStatement))
        {
            throw new InvalidOperationException("The audience artifact is incomplete.");
        }
        var evidence = input.EvidenceItemIds.ToHashSet();
        var boundEvidence = bindings
            .Where(binding => binding.FieldPath == "artifact.audiences")
            .SelectMany(binding => binding.EvidenceItemIds)
            .ToHashSet();
        var geographies = input.Geographies.ToHashSet(StringComparer.Ordinal);
        if (artifact.Audiences.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                item.Geographies is null || item.Exclusions is null ||
                item.EvidenceItemIds is null ||
                item.Language is not null || item.LifeStage is not null ||
                item.LsmSem is not null ||
                item.Geographies.Any(value => !geographies.Contains(value)) ||
                item.EvidenceItemIds.Any(value => !evidence.Contains(value)) ||
                item.EvidenceItemIds.Any(value => !boundEvidence.Contains(value)) ||
                (item.Classification != MasterDataCodes.EvidenceClassifications.Hypothesis &&
                    item.EvidenceItemIds.Length == 0) ||
                item.Confidence is < 0 or > 1 ||
                !IsClassification(item.Classification)))
        {
            throw new InvalidOperationException("The audience artifact contains unapproved facts.");
        }
    }

    private static void ValidateMediaMixArtifact(
        MediaMixArtifact artifact,
        MediaPlanningInput input)
    {
        if (artifact.Allocations is null || artifact.Allocations.Length == 0 ||
            artifact.Assumptions is null)
        {
            throw new InvalidOperationException("The media-mix artifact is incomplete.");
        }
        var allowed = input.AvailableChannels.ToHashSet(StringComparer.Ordinal);
        var channels = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var allocation in artifact.Allocations)
        {
            if (allocation is null || !allowed.Contains(allocation.Channel) ||
                !channels.Add(allocation.Channel) || allocation.BudgetMinor < 0 ||
                string.IsNullOrWhiteSpace(allocation.Role))
            {
                throw new InvalidOperationException(
                    "The media-mix artifact contains an invalid allocation.");
            }
            total = checked(total + allocation.BudgetMinor);
        }
        if (total != input.BudgetMinor)
        {
            throw new InvalidOperationException(
                "The media-mix artifact does not reconcile to the Brief budget.");
        }
    }

    private static bool IsClassification(string classification) =>
        classification is MasterDataCodes.EvidenceClassifications.Fact
            or MasterDataCodes.EvidenceClassifications.Inference
            or MasterDataCodes.EvidenceClassifications.Hypothesis;

    private static void EnsureCompleted(string status)
    {
        if (status != MasterDataCodes.LifecycleStatuses.Completed)
        {
            throw new InvalidOperationException("The planning agent did not complete its proposal.");
        }
    }

    private sealed record AudienceRequest(
        AgentInvocationRequest Invocation,
        AudienceContext Planning);

    private sealed record MediaPlanningRequest(
        AgentInvocationRequest Invocation,
        MediaPlanningContext Planning);

    private record AudienceContext(
        Guid BriefVersionId,
        string Objective,
        IReadOnlyList<string> Audiences,
        IReadOnlyList<string> Geographies);

    private sealed record MediaPlanningContext(
        Guid BriefVersionId,
        string Objective,
        IReadOnlyList<string> Audiences,
        IReadOnlyList<string> Geographies,
        long BudgetMinor,
        string Currency,
        IReadOnlyList<string> AvailableChannels) : AudienceContext(
            BriefVersionId, Objective, Audiences, Geographies);

    private sealed class AudienceArtifact
    {
        public required AudienceDefinition[] Audiences { get; init; }
        public required string TargetingRationale { get; init; }
        public required string PositioningStatement { get; init; }
    }

    private sealed class AudienceDefinition
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string NeedState { get; init; }
        public required string BuyingContext { get; init; }
        public required string[] Geographies { get; init; }
        public string? Language { get; init; }
        public string? LifeStage { get; init; }
        public string? LsmSem { get; init; }
        public required string Classification { get; init; }
        public required string[] Exclusions { get; init; }
        public required Guid[] EvidenceItemIds { get; init; }
        public required decimal Confidence { get; init; }
        public required bool IsTarget { get; init; }
    }

    private sealed class MediaMixArtifact
    {
        public required MediaAllocation[] Allocations { get; init; }
        public required string[] Assumptions { get; init; }
    }

    private sealed class MediaAllocation
    {
        public required string Channel { get; init; }
        public required long BudgetMinor { get; init; }
        public required string Role { get; init; }
    }
}
