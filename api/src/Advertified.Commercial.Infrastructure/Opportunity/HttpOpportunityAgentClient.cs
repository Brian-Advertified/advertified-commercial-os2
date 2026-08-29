using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Constants;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.Opportunity;

public sealed class HttpOpportunityAgentClient(
    HttpClient httpClient,
    IOptions<AgentRuntimeOptions> options) : IOpportunityAgentClient
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<OpportunityAgentOutput> InvokeAsync(
        OpportunityAgentInput input,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1/agents/{Uri.EscapeDataString(input.AgentCode)}")
        {
            Content = JsonContent.Create(CreatePayload(input), options: WireJson),
        };
        request.Headers.Add("X-Advertified-Service-Key", settings.ServiceKey);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(cancellationToken));
        return ParseAndValidate(document.RootElement, input);
    }

    private static object CreatePayload(OpportunityAgentInput input) => new
    {
        invocation = new
        {
            schema_version = "1.0.0",
            tenant_id = input.TenantId,
            actor_id = input.ActorId,
            effective_role = MasterDataCodes.Roles.AgentRuntimeService,
            run_id = input.RunId,
            step_id = input.StepId,
            correlation_id = input.CorrelationId,
            agent_code = input.AgentCode,
            contract_version = "1.0.0",
            prompt_version = "1.0.0",
            resource_refs = new[]
            {
                new
                {
                    resource_type = "EvidenceSet",
                    resource_id = input.EvidenceSetId,
                    version = input.EvidenceSetVersion,
                },
            },
            approved_evidence_item_ids = input.ApprovedEvidence.Select(item => item.Id),
            locale = "en-ZA",
            account_policy_version = "1.0.0",
            tool_policy = new
            {
                allowed_tools = Array.Empty<string>(),
                max_tool_calls = 0,
                consequence_policy = "PROPOSE_ONLY",
            },
            provider_policy = new
            {
                provider = "deterministic",
                model = "fixture-v1",
                temperature = 0,
                timeout_seconds = 30,
                max_attempts = 1,
                cost_cap_minor = 0,
                allow_live = false,
            },
            resume = new
            {
                checkpoint_id = (Guid?)null,
                prior_validated_output_ref = (object?)null,
                prior_usage_ref = (object?)null,
            },
        },
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

    private static OpportunityAgentOutput ParseAndValidate(
        JsonElement root,
        OpportunityAgentInput input)
    {
        var usage = root.GetProperty("usage");
        var usageOutput = new AgentUsageOutput(
            usage.GetProperty("provider").GetString()!,
            usage.GetProperty("model").GetString()!,
            usage.GetProperty("units").GetInt32(),
            usage.GetProperty("tool_calls").GetInt32(),
            usage.GetProperty("incremental_cost_minor").GetInt64(),
            usage.GetProperty("cache_status").GetString()!);
        if (usageOutput.IncrementalCostMinor != 0 || usageOutput.ToolCalls != 0)
        {
            throw new InvalidOperationException("The configured opportunity provider exceeded its zero-cost policy.");
        }
        ValidateEvidenceBindings(root.GetProperty("evidence_bindings"), input);
        var objections = root.GetProperty("objections").EnumerateArray()
            .Select(item => new AgentObjectionOutput(
                item.GetProperty("severity").GetString()!,
                item.GetProperty("field_path").GetString()!,
                item.GetProperty("evidence_gap").GetString()!,
                item.GetProperty("recommended_resolution").GetString()!))
            .ToArray();
        return new OpportunityAgentOutput(
            root.GetProperty("schema_version").GetString()!,
            root.GetProperty("status").GetString()!,
            root.GetProperty("artifact").Clone(),
            root.GetProperty("evidence_bindings").Clone(),
            root.GetProperty("unknowns").Clone(),
            root.GetProperty("assumptions").Clone(),
            objections,
            root.GetProperty("rationale").GetString()!,
            usageOutput);
    }

    private static void ValidateEvidenceBindings(
        JsonElement bindings,
        OpportunityAgentInput input)
    {
        var approved = input.ApprovedEvidence.Select(item => item.Id).ToHashSet();
        var invalid = bindings.EnumerateArray()
            .SelectMany(item => item.GetProperty("evidence_item_ids").EnumerateArray())
            .Select(item => item.GetGuid())
            .Any(item => !approved.Contains(item));
        if (invalid)
        {
            throw new InvalidOperationException("Agent output referenced unapproved evidence.");
        }
    }
}
