using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Intelligence;

public sealed class HttpAudienceIntelligenceAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IAudienceIntelligenceAgentClient
{
    private const string BriefVersionResourceType = "BriefVersion";

    public async Task<AudienceAgentProposal> ProposeAudiencesAsync(
        AudienceIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        var problem = input.Problem;
        var agentCode = MasterDataCodes.AgentTypes.AudienceIntelligence;
        var payload = new AudienceRequest(
            CreateInvocation(problem, agentCode),
            new AudienceContext(
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
                problem.Currency,
                input.Evidence,
                input.ReferenceEvidence));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<AudienceArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            problem.EvidenceItemIds,
            cancellationToken);
        EnsureCompleted(output.Status);
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Audience Intelligence artifact is unavailable.");
        ValidateArtifact(artifact, input, output.EvidenceBindings);
        return new AudienceAgentProposal(
            artifact.Audiences.Select(ToProposal).ToArray(),
            artifact.TargetingRationale,
            artifact.PositioningStatement,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Rationale,
            IntelligenceUsage.FromRuntime(agentCode, output.Usage));
    }

    private AgentInvocationRequest CreateInvocation(CommercialProblemInput input, string agentCode) =>
        AgentRuntimeHttpSupport.CreateInvocation(
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
            options.Value);

    private static AudienceDefinitionProposal ToProposal(AudienceDefinition item) => new(
        item.Name,
        item.Description,
        item.NeedState,
        item.BuyingContext,
        item.Geographies,
        item.Language,
        item.LifeStage,
        item.LsmSem,
        item.LsmSemTaxonomy,
        item.LsmSemTaxonomyVersion,
        item.Classification,
        item.Exclusions,
        item.EvidenceItemIds,
        item.ReferenceObservationIds,
        item.Confidence,
        item.IsTarget,
        item.LsmSemMandatory);

    private static void ValidateArtifact(
        AudienceArtifact artifact,
        AudienceIntelligenceInput input,
        IReadOnlyList<AgentEvidenceBinding> bindings)
    {
        if (artifact.Audiences is null ||
            artifact.Audiences.Any(item => item is null || item.Geographies is null ||
                item.Exclusions is null || item.EvidenceItemIds is null))
            throw new InvalidOperationException("The Audience Intelligence artifact is incomplete.");

        var proposals = artifact.Audiences.Select(ToProposal).ToArray();
        var problem = input.Problem;
        AudienceIntelligenceValidator.Validate(
            proposals, problem.Geographies, problem.EvidenceItemIds,
            input.Evidence, input.ReferenceEvidence, problem.Audiences);
        AudienceEvidenceGuard.Validate(proposals, input.Evidence);
        var boundEvidence = bindings
            .Where(binding => binding.FieldPath == "artifact.audiences")
            .SelectMany(binding => binding.EvidenceItemIds)
            .ToHashSet();
        if (proposals.Any(item => item.EvidenceItemIds.Any(value => !boundEvidence.Contains(value))))
            throw new InvalidOperationException("The Audience Intelligence artifact contains unapproved facts.");
    }

    private static void EnsureCompleted(string status)
    {
        if (status != MasterDataCodes.LifecycleStatuses.Completed)
            throw new InvalidOperationException("Audience Intelligence did not complete its proposal.");
    }

    private sealed record AudienceRequest(
        AgentInvocationRequest Invocation,
        AudienceContext Planning);

    private sealed record AudienceContext(
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
        string? Currency,
        IReadOnlyList<AudienceEvidenceFact> AudienceEvidence,
        IReadOnlyList<ReferenceObservationFact> ReferenceEvidence);

    private sealed class AudienceArtifact
    {
        public required AudienceDefinition[] Audiences { get; init; }
        public string? TargetingRationale { get; init; }
        public string? PositioningStatement { get; init; }
    }

    private sealed class AudienceDefinition
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public string? NeedState { get; init; }
        public string? BuyingContext { get; init; }
        public required string[] Geographies { get; init; }
        public string? Language { get; init; }
        public string? LifeStage { get; init; }
        public string? LsmSem { get; init; }
        public string? LsmSemTaxonomy { get; init; }
        public string? LsmSemTaxonomyVersion { get; init; }
        public bool LsmSemMandatory { get; init; }
        public required string Classification { get; init; }
        public required string[] Exclusions { get; init; }
        public required Guid[] EvidenceItemIds { get; init; }
        public required Guid[] ReferenceObservationIds { get; init; }
        public required decimal? Confidence { get; init; }
        public required bool IsTarget { get; init; }
    }
}
