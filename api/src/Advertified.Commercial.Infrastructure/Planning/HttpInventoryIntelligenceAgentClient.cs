using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed class HttpInventoryIntelligenceAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IInventoryIntelligenceAgentClient
{
    private const string Operation = "INVENTORY_INTERPRETATION";

    public async Task<InventoryIntelligenceProposal> InterpretShortlistAsync(
        InventoryIntelligenceInput input,
        CancellationToken cancellationToken)
    {
        var agentCode = MasterDataCodes.AgentTypes.InventoryIntelligence;
        var resources = new AgentResourceReference[]
        {
            new("BriefVersion", input.BriefVersionId, input.BriefVersion),
            new("InventoryShortlistVersion", input.ShortlistVersionId, input.ShortlistVersion),
            new("IntelligenceArtifact", input.Strategy.AudienceArtifactId,
                input.Strategy.AudienceArtifactVersion),
            new("IntelligenceArtifact", input.Strategy.MediaStrategyArtifactId,
                input.Strategy.MediaStrategyArtifactVersion),
            new("MediaMixVersion", input.Strategy.MediaMixVersionId,
                input.Strategy.MediaMixVersion),
        };
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(
            input.TenantId,
            input.ActorId,
            input.RunId,
            Guid.NewGuid(),
            input.CorrelationId,
            agentCode,
            resources,
            input.EvidenceItemIds,
            options.Value,
            modelOperation: Operation);
        var payload = new InventoryIntelligenceRequest(
            Operation,
            invocation,
            new InventoryIntelligenceContext(
                input.BriefVersionId,
                input.ShortlistVersionId,
                input.Candidates,
                input.Strategy));
        var output = await AgentRuntimeHttpSupport.InvokeAsync<InventoryIntelligenceArtifact>(
            httpClient,
            options.Value,
            agentCode,
            payload,
            input.EvidenceItemIds,
            cancellationToken,
            Operation);
        if (output.Status != MasterDataCodes.LifecycleStatuses.Completed)
            throw new InvalidOperationException("Inventory Intelligence did not complete.");
        var artifact = output.Artifact
            ?? throw new InvalidOperationException("The Inventory Intelligence artifact is unavailable.");
        Validate(artifact, input);
        return new InventoryIntelligenceProposal(
            artifact.Interpretations,
            output.Unknowns.Select(item => item.Question).ToArray(),
            output.Assumptions.Select(item => item.Value).ToArray(),
            output.Rationale,
            IntelligenceUsage.FromRuntime(Operation, output.Usage));
    }

    private static void Validate(
        InventoryIntelligenceArtifact artifact,
        InventoryIntelligenceInput input)
    {
        if (artifact.Interpretations is null || artifact.Interpretations.Count == 0)
            throw new InvalidOperationException("Inventory Intelligence returned no interpretations.");

        var candidateIds = input.Candidates.Select(item => item.CandidateId).ToHashSet();
        var interpretedIds = artifact.Interpretations.Select(item => item.CandidateId).ToArray();
        if (interpretedIds.Length != interpretedIds.Distinct().Count() ||
            !interpretedIds.ToHashSet().SetEquals(candidateIds))
            throw new InvalidOperationException("Inventory Intelligence did not interpret every candidate exactly once.");

        foreach (var interpretation in artifact.Interpretations)
        {
            if (string.IsNullOrWhiteSpace(interpretation.Rationale) ||
                interpretation.Rationale.Length > 1_000 ||
                interpretation.Classification != MasterDataCodes.EvidenceClassifications.AiRecommendation)
                throw new InvalidOperationException("Inventory Intelligence returned an invalid advisory interpretation.");
        }
    }

    private sealed record InventoryIntelligenceRequest(
        string Operation,
        AgentInvocationRequest Invocation,
        InventoryIntelligenceContext Inventory);

    private sealed record InventoryIntelligenceContext(
        Guid BriefVersionId,
        Guid ShortlistVersionId,
        IReadOnlyList<InventoryCandidateInput> Candidates,
        InventoryStrategyInput Strategy);
}
