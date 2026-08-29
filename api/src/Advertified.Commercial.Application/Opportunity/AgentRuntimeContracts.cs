using System.Text.Json;

namespace Advertified.Commercial.Application.Opportunity;

public sealed record AgentEvidenceInput(
    Guid Id,
    string ClaimType,
    JsonElement StructuredValue,
    string Excerpt);

public sealed record AgentPriorArtifactInput(
    string ArtifactType,
    Guid ArtifactId,
    int Version,
    JsonElement Value);

public sealed record OpportunityAgentInput(
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    Guid StepId,
    Guid CorrelationId,
    string AgentCode,
    Guid OpportunityId,
    string OpportunityTitle,
    string? ProblemSummary,
    string? ObjectiveSummary,
    Guid EvidenceSetId,
    int EvidenceSetVersion,
    IReadOnlyList<AgentEvidenceInput> ApprovedEvidence,
    IReadOnlyList<AgentPriorArtifactInput> PriorArtifacts);

public sealed record AgentObjectionOutput(
    string Severity,
    string FieldPath,
    string EvidenceGap,
    string RecommendedResolution);

public sealed record AgentUsageOutput(
    string Provider,
    string Model,
    int Units,
    int ToolCalls,
    long IncrementalCostMinor,
    string CacheStatus);

public sealed record OpportunityAgentOutput(
    string SchemaVersion,
    string Status,
    JsonElement Artifact,
    JsonElement EvidenceBindings,
    JsonElement Unknowns,
    JsonElement Assumptions,
    IReadOnlyList<AgentObjectionOutput> Objections,
    string Rationale,
    AgentUsageOutput Usage);

public interface IOpportunityAgentClient
{
    Task<OpportunityAgentOutput> InvokeAsync(
        OpportunityAgentInput input,
        CancellationToken cancellationToken);
}
