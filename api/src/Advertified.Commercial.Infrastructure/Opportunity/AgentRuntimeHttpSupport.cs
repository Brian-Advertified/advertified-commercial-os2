using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal static class AgentRuntimeHttpSupport
{
    private const string SchemaVersion = "1.0.0";
    private const string UndefinedLocale = "und";
    private const string ServiceKeyHeader = "X-Advertified-Service-Key";

    internal static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    internal static AgentInvocationRequest CreateInvocation(
        Guid tenantId,
        Guid actorId,
        Guid runId,
        Guid stepId,
        Guid correlationId,
        string agentCode,
        string resourceType,
        Guid resourceId,
        long resourceVersion,
        IReadOnlyList<Guid> evidenceItemIds,
        AgentRuntimeOptions settings) => CreateInvocation(
            tenantId,
            actorId,
            runId,
            stepId,
            correlationId,
            agentCode,
            [new AgentResourceReference(resourceType, resourceId, resourceVersion)],
            evidenceItemIds,
            settings);

    internal static AgentInvocationRequest CreateInvocation(
        Guid tenantId,
        Guid actorId,
        Guid runId,
        Guid stepId,
        Guid correlationId,
        string agentCode,
        IReadOnlyList<AgentResourceReference> resourceRefs,
        IReadOnlyList<Guid> evidenceItemIds,
        AgentRuntimeOptions settings)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty || runId == Guid.Empty ||
            stepId == Guid.Empty || correlationId == Guid.Empty ||
            resourceRefs.Count == 0 || resourceRefs.Any(reference =>
                reference.ResourceId == Guid.Empty || reference.Version <= 0))
        {
            throw new ArgumentException("The agent invocation resource context is invalid.");
        }
        return new AgentInvocationRequest(
            SchemaVersion,
            tenantId,
            actorId,
            MasterDataCodes.Roles.AgentRuntimeService,
            runId,
            stepId,
            correlationId,
            agentCode,
            SchemaVersion,
            SchemaVersion,
            resourceRefs,
            evidenceItemIds,
            UndefinedLocale,
            SchemaVersion,
            new AgentToolPolicy([], 0, "PROPOSE_ONLY"),
            CreateProviderPolicy(settings, agentCode),
            new AgentResumeContext(null, null, null));
    }

    private static AgentProviderPolicy CreateProviderPolicy(
        AgentRuntimeOptions settings,
        string agentCode)
    {
        if (!AgentRuntimeOptions.HasSafeProviderPolicy(settings) ||
            !AgentRuntimeOptions.HasSafeRoutes(settings))
        {
            throw new InvalidOperationException("The agent provider policy is unsafe.");
        }
        var model = settings.ModelFor(agentCode);
        var costCap = settings.CostCapFor(agentCode);
        if (settings.Provider == AgentRuntimeOptions.BedrockProvider &&
            (!settings.AllowLive || costCap <= 0))
        {
            throw new InvalidOperationException(
                "The live agent route is not activated or has no positive cost cap.");
        }
        return new AgentProviderPolicy(
            settings.Provider,
            model,
            0,
            settings.TimeoutSeconds,
            settings.MaxAttempts,
            costCap,
            settings.AllowLive);
    }

    internal static async Task<AgentRuntimeResponse<TArtifact>> InvokeAsync<TArtifact>(
        HttpClient httpClient,
        AgentRuntimeOptions settings,
        string agentCode,
        object payload,
        IReadOnlyList<Guid> approvedEvidenceItemIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceKey))
        {
            throw new InvalidOperationException("The agent runtime service key is unavailable.");
        }
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1/agents/{Uri.EscapeDataString(agentCode)}")
        {
            Content = JsonContent.Create(payload, options: WireJson),
        };
        request.Headers.Add(ServiceKeyHeader, settings.ServiceKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var failure = await response.Content.ReadAsStringAsync(
                cancellationToken);
            throw AgentRuntimeRejectedException.Read(
                response.StatusCode, failure);
        }
        var output = await response.Content.ReadFromJsonAsync<AgentRuntimeResponse<TArtifact>>(
            WireJson, cancellationToken)
            ?? throw new JsonException("The agent runtime returned an empty response.");
        Validate(output, settings, agentCode, approvedEvidenceItemIds);
        return output;
    }

    private static void Validate<TArtifact>(
        AgentRuntimeResponse<TArtifact> output,
        AgentRuntimeOptions settings,
        string agentCode,
        IReadOnlyList<Guid> approvedEvidenceItemIds)
    {
        ValidateEnvelope(output);
        ValidateUsage(output.Usage, CreateProviderPolicy(settings, agentCode));
        ValidateEvidence(output.EvidenceBindings, approvedEvidenceItemIds);
        ValidateMetadata(output);
    }

    private static void ValidateEnvelope<TArtifact>(AgentRuntimeResponse<TArtifact> output)
    {
        if (output.SchemaVersion != SchemaVersion || output.Artifact is null ||
            output.Status is not (MasterDataCodes.LifecycleStatuses.Completed or
                MasterDataCodes.LifecycleStatuses.ReviewRequired) ||
            string.IsNullOrWhiteSpace(output.Rationale) || output.EvidenceBindings is null ||
            output.Unknowns is null || output.Assumptions is null || output.Confidence is null ||
            output.Objections is null || output.Usage is null)
        {
            throw new InvalidOperationException("The agent runtime response is incomplete.");
        }
        if (output.Artifact is JsonElement element &&
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("The agent runtime response has no artifact.");
        }
    }

    private static void ValidateUsage(
        AgentProviderUsage usage,
        AgentProviderPolicy expected)
    {
        if (usage.Provider != expected.Provider || usage.Model != expected.Model ||
            usage.Units < 0 || usage.ToolCalls != 0 || usage.IncrementalCostMinor < 0 ||
            usage.IncrementalCostMinor > expected.CostCapMinor)
        {
            throw new InvalidOperationException("The agent provider usage violated its policy.");
        }
        if (expected.Provider == AgentRuntimeOptions.DeterministicProvider)
        {
            if (usage.Units != 0 || usage.IncrementalCostMinor != 0 ||
                usage.CacheStatus != AgentProviderMetadata.FixtureCacheStatus ||
                usage.ProviderRequestId is not null ||
                usage.InputTokens != 0 || usage.OutputTokens != 0 ||
                usage.IncrementalCostUsdMicros != 0)
            {
                throw new InvalidOperationException(
                    "The deterministic agent provider exceeded its zero-cost policy.");
            }
            return;
        }
        if (usage.Units <= 0 ||
            usage.CacheStatus is not (AgentProviderMetadata.LiveCacheStatus or
                AgentProviderMetadata.CacheHitStatus) ||
            string.IsNullOrWhiteSpace(usage.ProviderRequestId) ||
            usage.InputTokens < 0 || usage.OutputTokens <= 0 ||
            usage.Units != usage.InputTokens + usage.OutputTokens ||
            usage.IncrementalCostUsdMicros <= 0)
        {
            throw new InvalidOperationException(
                "The live agent provider usage is incomplete.");
        }
    }

    private static void ValidateEvidence(
        IReadOnlyList<AgentEvidenceBinding> bindings,
        IReadOnlyList<Guid> approvedEvidenceItemIds)
    {
        var approved = approvedEvidenceItemIds.ToHashSet();
        if (bindings.Any(binding =>
                binding is null || string.IsNullOrWhiteSpace(binding.FieldPath) ||
                binding.EvidenceItemIds is null || binding.EvidenceItemIds.Length == 0 ||
                binding.EvidenceItemIds.Any(item => !approved.Contains(item))))
        {
            throw new InvalidOperationException("Agent output referenced unapproved evidence.");
        }
    }

    private static void ValidateMetadata<TArtifact>(AgentRuntimeResponse<TArtifact> output)
    {
        if (output.Unknowns.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.FieldPath) ||
                string.IsNullOrWhiteSpace(item.Question)) ||
            output.Assumptions.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.FieldPath) ||
                string.IsNullOrWhiteSpace(item.Value)) ||
            output.Confidence.Any(item => item is null || item.Confidence is < 0 or > 1) ||
            output.Objections.Any(item => item is null ||
                string.IsNullOrWhiteSpace(item.Severity) ||
                string.IsNullOrWhiteSpace(item.FieldPath)))
        {
            throw new InvalidOperationException("The agent runtime metadata is invalid.");
        }
    }
}

internal sealed record AgentInvocationRequest(
    string SchemaVersion,
    Guid TenantId,
    Guid ActorId,
    string EffectiveRole,
    Guid RunId,
    Guid StepId,
    Guid CorrelationId,
    string AgentCode,
    string ContractVersion,
    string PromptVersion,
    IReadOnlyList<AgentResourceReference> ResourceRefs,
    IReadOnlyList<Guid> ApprovedEvidenceItemIds,
    string Locale,
    string AccountPolicyVersion,
    AgentToolPolicy ToolPolicy,
    AgentProviderPolicy ProviderPolicy,
    AgentResumeContext Resume);

internal sealed record AgentResourceReference(
    string ResourceType,
    Guid ResourceId,
    long Version);

internal sealed record AgentToolPolicy(
    IReadOnlyList<string> AllowedTools,
    int MaxToolCalls,
    string ConsequencePolicy);

internal sealed record AgentProviderPolicy(
    string Provider,
    string Model,
    int Temperature,
    int TimeoutSeconds,
    int MaxAttempts,
    long CostCapMinor,
    bool AllowLive);

internal sealed record AgentResumeContext(
    Guid? CheckpointId,
    object? PriorValidatedOutputRef,
    object? PriorUsageRef);

internal sealed class AgentRuntimeResponse<TArtifact>
{
    public required string SchemaVersion { get; init; }
    public required string Status { get; init; }
    public required TArtifact? Artifact { get; init; }
    public required AgentEvidenceBinding[] EvidenceBindings { get; init; }
    public required AgentUnknown[] Unknowns { get; init; }
    public required AgentAssumption[] Assumptions { get; init; }
    public required AgentConfidence[] Confidence { get; init; }
    public required AgentObjection[] Objections { get; init; }
    public required string Rationale { get; init; }
    public AgentSuggestedNextAction? SuggestedNextAction { get; init; }
    public required AgentProviderUsage Usage { get; init; }
}

internal sealed record AgentEvidenceBinding(
    string FieldPath,
    Guid[] EvidenceItemIds);

internal sealed record AgentUnknown(
    string FieldPath,
    string Question,
    bool IsBlocking);

internal sealed record AgentAssumption(
    string FieldPath,
    string Value,
    string Impact,
    string ValidationNeeded);

internal sealed record AgentConfidence(
    string FieldPath,
    decimal Confidence);

internal sealed record AgentObjection(
    string Severity,
    string FieldPath,
    string EvidenceGap,
    string RecommendedResolution);

internal sealed record AgentSuggestedNextAction(
    string CommandCode,
    bool RequiresHuman);

internal sealed record AgentProviderUsage(
    string Provider,
    string Model,
    int Units,
    int ToolCalls,
    long IncrementalCostMinor,
    string CacheStatus,
    string? ProviderRequestId = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    long IncrementalCostUsdMicros = 0);
