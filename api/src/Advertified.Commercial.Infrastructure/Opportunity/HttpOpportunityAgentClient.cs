using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class HttpOpportunityAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IOpportunityAgentClient
{
    public async Task<OpportunityAgentOutput> InvokeAsync(
        OpportunityAgentInput input,
        CancellationToken cancellationToken)
    {
        var output = await AgentRuntimeHttpSupport.InvokeAsync<JsonElement>(
            httpClient,
            options.Value,
            input.AgentCode,
            CreatePayload(input, options.Value),
            input.ApprovedEvidence.Select(item => item.Id).ToArray(),
            cancellationToken);
        return ToOutput(output);
    }

    private static object CreatePayload(
        OpportunityAgentInput input,
        AgentRuntimeOptions settings) => new
    {
        invocation = AgentRuntimeHttpSupport.CreateInvocation(
            input.TenantId,
            input.ActorId,
            input.RunId,
            input.StepId,
            input.CorrelationId,
            input.AgentCode,
            "EvidenceSet",
            input.EvidenceSetId,
            input.EvidenceSetVersion,
            input.ApprovedEvidence.Select(item => item.Id).ToArray(),
            settings),
        opportunity = new
        {
            id = input.OpportunityId,
            title = input.OpportunityTitle,
            problem_summary = input.ProblemSummary,
            objective_summary = input.ObjectiveSummary,
        },
        approved_evidence = input.ApprovedEvidence.Select(item => new
        {
            id = item.Id,
            claim_type = item.ClaimType,
            structured_value = item.StructuredValue,
            excerpt = item.Excerpt,
        }),
        prior_artifacts = input.PriorArtifacts.Select(item => new
        {
            artifact_type = item.ArtifactType,
            artifact_id = item.ArtifactId,
            version = item.Version,
            value = item.Value,
        }),
    };

    private static OpportunityAgentOutput ToOutput(
        AgentRuntimeResponse<JsonElement> output)
    {
        var usageOutput = new AgentUsageOutput(
            output.Usage.Provider,
            output.Usage.Model,
            output.Usage.Units,
            output.Usage.ToolCalls,
            output.Usage.IncrementalCostMinor,
            output.Usage.CacheStatus,
            output.Usage.ProviderRequestId);
        var objections = output.Objections
            .Select(item => new AgentObjectionOutput(
                item.Severity,
                item.FieldPath,
                item.EvidenceGap,
                item.RecommendedResolution))
            .ToArray();
        return new OpportunityAgentOutput(
            output.SchemaVersion,
            output.Status,
            output.Artifact,
            JsonSerializer.SerializeToElement(
                output.EvidenceBindings, AgentRuntimeHttpSupport.WireJson),
            JsonSerializer.SerializeToElement(
                output.Unknowns, AgentRuntimeHttpSupport.WireJson),
            JsonSerializer.SerializeToElement(
                output.Assumptions, AgentRuntimeHttpSupport.WireJson),
            objections,
            output.Rationale,
            usageOutput);
    }
}
