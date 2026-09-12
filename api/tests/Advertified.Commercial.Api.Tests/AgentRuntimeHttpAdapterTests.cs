using System.Net;
using System.Text;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Infrastructure.Intelligence;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    private static readonly Guid EvidenceId = Guid.Parse(
        "77777777-7777-7777-7777-777777777777");
    private static readonly Guid BriefVersionId = Guid.Parse(
        "66666666-6666-6666-6666-666666666666");
    private static readonly string[] CustomerGroups = ["Small businesses"];

    [Fact]
    public async Task OpportunityAdapterUsesSharedValidatedRuntimeBoundary()
    {
        var stepId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var client = CreateClient(async request =>
        {
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStreamAsync(CancellationToken.None));
            Assert.Equal(stepId,
                body.RootElement.GetProperty("invocation").GetProperty("step_id").GetGuid());
            return Response(new
            {
                offering = "Furniture",
                customer_groups = CustomerGroups,
            }, [EvidenceId]);
        });
        var adapter = new HttpOpportunityAgentClient(client, Settings());
        var input = new OpportunityAgentInput(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            stepId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "business_interpretation",
            Guid.Parse("88888888-8888-8888-8888-888888888888"),
            "Local opportunity",
            null,
            null,
            Guid.Parse("99999999-9999-9999-9999-999999999999"),
            2,
            [new AgentEvidenceInput(
                EvidenceId,
                "BUSINESS_CONTEXT",
                JsonSerializer.SerializeToElement(new { statement = "Approved context" }),
                "Approved fixture evidence.")],
            []);

        var result = await adapter.InvokeAsync(input, CancellationToken.None);

        Assert.Equal("Furniture", result.Artifact.GetProperty("offering").GetString());
        Assert.Equal(0, result.Usage.IncrementalCostMinor);
    }

    [Fact]
    public async Task AudienceAdapterSendsExactBriefAndMapsValidatedArtifact()
    {
        var client = CreateClient(async request =>
        {
            Assert.Equal("/v1/agents/audience_intelligence", request.RequestUri!.AbsolutePath);
            Assert.Equal("local-service-key",
                request.Headers.GetValues("X-Advertified-Service-Key").Single());
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStreamAsync(CancellationToken.None));
            var invocation = body.RootElement.GetProperty("invocation");
            Assert.Equal(BriefVersionId,
                invocation.GetProperty("resource_refs")[0].GetProperty("resource_id").GetGuid());
            return Response(
                AudienceArtifact(), [EvidenceId], fieldPath: "artifact.audiences");
        });
        var adapter = new HttpAudienceIntelligenceAgentClient(client, Settings());

        var result = await adapter.ProposeAudiencesAsync(
            AudienceInput(), CancellationToken.None);

        var audience = Assert.Single(result.Audiences);
        Assert.Equal("Furniture buyers", audience.Name);
        Assert.Equal([EvidenceId], audience.EvidenceItemIds);
        Assert.Equal(0, result.Usage.IncrementalCostMinor);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AudienceAdapterRejectsCostOrUnapprovedEvidence(bool nonZeroCost)
    {
        var boundEvidence = nonZeroCost
            ? new[] { EvidenceId }
            : new[] { Guid.Parse("99999999-9999-9999-9999-999999999999") };
        var client = CreateClient(_ => Task.FromResult(
            Response(
                AudienceArtifact(), boundEvidence, nonZeroCost ? 1 : 0,
                "artifact.audiences")));
        var adapter = new HttpAudienceIntelligenceAgentClient(client, Settings());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.ProposeAudiencesAsync(AudienceInput(), CancellationToken.None));
    }

    [Fact]
    public async Task AudienceAdapterRejectsUnknownResponseFields()
    {
        var response = Response(
            AudienceArtifact(), [EvidenceId], fieldPath: "artifact.audiences");
        response.Content = JsonString(
            (await response.Content.ReadAsStringAsync(CancellationToken.None))
                .Replace("\"usage\":", "\"unexpected\":true,\"usage\":",
                    StringComparison.Ordinal));
        var client = CreateClient(_ => Task.FromResult(response));
        var adapter = new HttpAudienceIntelligenceAgentClient(client, Settings());

        await Assert.ThrowsAsync<JsonException>(() =>
            adapter.ProposeAudiencesAsync(AudienceInput(), CancellationToken.None));
    }

    [Fact]
    public async Task AdapterPreservesRejectedProviderUsageAndStage()
    {
        var failure = JsonSerializer.Serialize(new
        {
            detail = new
            {
                provider_acceptance = "ACCEPTED",
                stage = "GROUNDING_VALIDATION",
                usage = new
                {
                    provider = "bedrock",
                    model = "amazon.nova-lite-v1:0",
                    units = 10_073,
                    tool_calls = 0,
                    incremental_cost_minor = 1,
                    cache_status = "LIVE",
                    provider_request_id = "request-123",
                    input_tokens = 8_667,
                    output_tokens = 1_406,
                    incremental_cost_usd_micros = 858,
                },
            },
        });
        var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonString(failure),
        }));
        var adapter = new HttpAudienceIntelligenceAgentClient(client, Settings());

        var rejected = await Assert.ThrowsAsync<AgentRuntimeRejectedException>(() =>
            adapter.ProposeAudiencesAsync(AudienceInput(), CancellationToken.None));

        Assert.Equal("ACCEPTED", rejected.Acceptance);
        Assert.Equal("GROUNDING_VALIDATION", rejected.Stage);
        Assert.Equal("request-123", rejected.ProviderRequestId);
        Assert.Equal(858, rejected.CostUsdMicros);
        Assert.Equal(1, rejected.IncrementalCostMinor);
        Assert.True(rejected.HasBillableAcceptedUsage);
    }

    private static object AudienceArtifact() => new
    {
        audiences = new[]
        {
            new
            {
                name = "Furniture buyers",
                description = "Brief-supplied audience: Furniture buyers. Unstated motivations and behaviours are not established.",
                need_state = (string?)null,
                buying_context = (string?)null,
                geographies = new[] { "Gauteng" },
                language = (string?)null,
                life_stage = (string?)null,
                lsm_sem = (string?)null,
                lsm_sem_taxonomy = (string?)null,
                lsm_sem_taxonomy_version = (string?)null,
                lsm_sem_mandatory = false,
                classification = "CLIENT_REQUIREMENT",
                exclusions = Array.Empty<string>(),
                evidence_item_ids = new[] { EvidenceId },
                reference_observation_ids = Array.Empty<Guid>(),
                confidence = 0.7m,
                is_target = true,
            },
        },
        targeting_rationale = "Prioritise the supplied audience for the stated objective.",
        positioning_statement = (string?)null,
    };

    private static AudienceIntelligenceInput AudienceInput() => new(
        new CommercialProblemInput(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            BriefVersionId,
            3,
            "Furniture Client",
            "Increase furniture enquiries.",
            "Increase enquiries",
            ["Furniture buyers"],
            ["Gauteng"],
            [],
            [],
            [],
            [],
            null,
            null,
            [EvidenceId]),
        [new AudienceEvidenceFact(
            EvidenceId,
            "Furniture buyers",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null)],
        []);

    private static IOptions<AgentRuntimeOptions> Settings() => Options.Create(new AgentRuntimeOptions
    {
        Mode = AgentRuntimeOptions.HttpDeterministicMode,
        BaseUrl = "http://agent-runtime.test",
        ServiceKey = "local-service-key",
        Provider = AgentRuntimeOptions.DeterministicProvider,
        DefaultModel = "fixture-v1",
        DefaultCostCapMinor = 0,
        AllowLive = false,
        MaxAttempts = 1,
    });

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> send) =>
        new(new StubHandler(send)) { BaseAddress = new Uri("http://agent-runtime.test") };

    private static StringContent JsonString(string value)
    {
        var content = new StringContent(value, Encoding.UTF8);
        content.Headers.ContentType = new("application/json");
        return content;
    }

    private static HttpResponseMessage Response(
        object artifact,
        Guid[] evidenceIds,
        long incrementalCostMinor = 0,
        string fieldPath = "artifact")
    {
        var json = JsonSerializer.Serialize(new
        {
            schema_version = "1.0.0",
            status = "COMPLETED",
            artifact,
            evidence_bindings = evidenceIds.Length == 0 ? [] : new[]
            {
                new { field_path = fieldPath, evidence_item_ids = evidenceIds },
            },
            unknowns = Array.Empty<object>(),
            assumptions = Array.Empty<object>(),
            confidence = Array.Empty<object>(),
            objections = Array.Empty<object>(),
            rationale = "Uses only supplied approved facts.",
            suggested_next_action = new
            {
                command_code = "Review",
                requires_human = true,
            },
            usage = new
            {
                provider = "deterministic",
                model = "fixture-v1",
                units = 0,
                tool_calls = 0,
                incremental_cost_minor = incrementalCostMinor,
                cache_status = "FIXTURE",
                provider_request_id = (string?)null,
                input_tokens = 0,
                output_tokens = 0,
                incremental_cost_usd_micros = 0,
            },
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) :
        HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
