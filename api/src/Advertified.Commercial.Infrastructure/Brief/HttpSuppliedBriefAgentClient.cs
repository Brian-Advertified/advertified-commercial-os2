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
    private const string Operation = "SUPPLIED_BRIEF_UNDERSTANDING";
    private const string PromptVersion = "1.2.0";
    private const string InputReferenceType = "SuppliedBriefInput";

    public async Task<SuppliedBriefUnderstandingView> UnderstandAsync(
        SuppliedBriefAgentInput input, CancellationToken cancellationToken)
    {
        var sourceHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input.SourceContent)));
        var requestId = input.Interpretation?.Id ?? Guid.NewGuid();
        var invocation = AgentRuntimeHttpSupport.CreateInvocation(input.TenantId, input.ActorId,
            requestId, requestId, requestId, MasterDataCodes.AgentTypes.BriefDrafting,
            InputReferenceType, requestId, input.Interpretation?.Version ?? 1, [], options.Value,
            PromptVersion, Operation);
        var payload = new { Operation, Invocation = invocation,
            Source = new { input.SourceTitle, input.SourceContent, SourceHash = sourceHash, input.Clarifications } };
        AgentRuntimeResponse<SuppliedBriefArtifact> response;
        try
        {
            response = await AgentRuntimeHttpSupport.InvokeAsync<SuppliedBriefArtifact>(
                client, options.Value, MasterDataCodes.AgentTypes.BriefDrafting, payload, [],
                cancellationToken, Operation);
        }
        catch (AgentRuntimeRejectedException rejected)
        {
            var settings = options.Value;
            if (!rejected.HasBillableAcceptedUsage ||
                rejected.Provider != settings.Provider ||
                rejected.Model != settings.ModelFor(
                    MasterDataCodes.AgentTypes.BriefDrafting, Operation) ||
                rejected.Units != rejected.InputTokens + rejected.OutputTokens ||
                rejected.ToolCalls != 0 ||
                rejected.IncrementalCostMinor > settings.CostCapFor(
                    MasterDataCodes.AgentTypes.BriefDrafting))
            {
                throw;
            }
            var rejectedUsage = new SuppliedBriefAgentUsageView(
                rejected.Provider!, rejected.Model!, PromptVersion, "NOT_REQUESTED",
                rejected.ToolCalls!.Value, rejected.IncrementalCostMinor!.Value,
                rejected.Units!.Value, rejected.CacheStatus, rejected.ProviderRequestId,
                rejected.InputTokens!.Value, rejected.OutputTokens!.Value,
                rejected.CostUsdMicros!.Value);
            throw new SuppliedBriefValidationException(
                rejectedUsage, rejected.ResponseJson, rejected);
        }
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
            var source = evidence.SourceLocator == "supplied:title" ? input.SourceTitle :
                evidence.SourceLocator.StartsWith("supplied:brief/", StringComparison.Ordinal)
                    ? input.SourceContent
                    : input.Clarifications.LastOrDefault(item =>
                        evidence.SourceLocator == $"clarification:{item.FieldPath}")?.Value;
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
