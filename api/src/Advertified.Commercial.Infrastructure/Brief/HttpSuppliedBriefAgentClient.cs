using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class HttpSuppliedBriefAgentClient(
    HttpClient client, IOptions<AgentRuntimeOptions> options) : ISuppliedBriefAgentClient
{
    public bool IsAvailable => options.Value.UsesHttp;

    private const string Operation = "SUPPLIED_BRIEF_UNDERSTANDING";
    private const string PromptVersion = "1.0.0";
    private const string InputReferenceType = "SuppliedBriefInput";

    public async Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        if (!options.Value.UsesHttp) throw new SuppliedBriefInterpretationUnavailableException();
        var sourceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.SourceContent)));
        var requestId = input.Interpretation?.Id ?? Guid.NewGuid();
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(input.TenantId, input.ActorId,
            requestId, requestId, requestId, MasterDataCodes.AgentTypes.BriefDrafting,
            InputReferenceType, requestId, input.Interpretation?.Version ?? 1, [], options.Value);
        var payload = new { Operation, Invocation = invocation,
            Source = new { input.SourceTitle, input.SourceContent, SourceHash = sourceHash, input.Clarifications } };
        var response = await AgentRuntimeHttpSupport.InvokeAsync<SuppliedBriefArtifact>(
            client, options.Value, MasterDataCodes.AgentTypes.BriefDrafting, payload, [], cancellationToken);
        var artifact = response.Artifact ?? throw new InvalidOperationException("Brief interpretation is incomplete.");
        var usage = new SuppliedBriefAgentUsageView(response.Usage.Provider, response.Usage.Model, PromptVersion,
            "NOT_REQUESTED", response.Usage.ToolCalls, response.Usage.IncrementalCostMinor,
            response.Usage.Units, response.Usage.CacheStatus, response.Usage.ProviderRequestId,
            response.Usage.InputTokens, response.Usage.OutputTokens, response.Usage.IncrementalCostUsdMicros);
        try { ValidateGrounding(artifact, input, sourceHash); }
        catch (InvalidOperationException failure)
        {
            throw new SuppliedBriefValidationException(usage,
                JsonSerializer.Serialize(response, AgentRuntimeHttpSupport.WireJson), failure);
        }
        return new SuppliedBriefUnderstandingView(artifact.ClientName, artifact.Title, artifact.CampaignMode,
            artifact.CampaignModeConfidence, artifact.RequiresHumanClarification, artifact.CampaignModeRationale,
            artifact.Draft, artifact.Questions, artifact.Evidence,
            usage);
    }

    private static void ValidateGrounding(SuppliedBriefArtifact artifact, SuppliedBriefAgentInput input, string hash)
    {
        if (artifact.SourceHash != hash || artifact.Draft is null || artifact.Questions is null ||
            artifact.Evidence is null || string.IsNullOrWhiteSpace(artifact.Title))
            throw new InvalidOperationException("Brief interpretation must bind the exact source.");
        foreach (var evidence in artifact.Evidence)
        {
            var source = evidence.SourceLocator == "supplied:brief" ? input.SourceContent :
                input.Clarifications.LastOrDefault(item => evidence.SourceLocator == $"clarification:{item.FieldPath}")?.Value;
            if (string.IsNullOrEmpty(evidence.Excerpt) || source is null ||
                !source.Contains(evidence.Excerpt, StringComparison.Ordinal))
                throw new InvalidOperationException("Brief evidence is not grounded in its declared source.");
        }
        if (artifact.RequiresHumanClarification != artifact.Questions.Any(item => item.IsBlocking) ||
            artifact.Draft.BudgetUnknown != (artifact.Draft.BudgetMinor is null))
            throw new InvalidOperationException("Brief interpretation has an inconsistent unknown disposition.");
    }

    private sealed record SuppliedBriefArtifact(
        string SourceHash, string? ClientName, string Title, string? CampaignMode,
        decimal CampaignModeConfidence, bool RequiresHumanClarification, string CampaignModeRationale,
        SuppliedBriefDraftView Draft, IReadOnlyList<SuppliedBriefQuestionView> Questions,
        IReadOnlyList<SuppliedBriefEvidenceView> Evidence);
}
