using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.Opportunity;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    private static readonly IReadOnlyList<string> BedrockRoutes =
    [
        "business_interpretation",
        "opportunity_intelligence",
        "strategy",
        "critic_readiness",
        "brief_drafting",
        "market_intelligence",
        "audience_intelligence",
        "location_intelligence",
        "media_strategy",
        "inventory_intelligence",
        "proposal_narrative",
        "creative",
        "measurement",
        "brief_drafting__supplied_brief_understanding",
        "inventory_intelligence__schema_discovery",
        "inventory_intelligence__source_transcription",
        "inventory_intelligence__semantic_enrichment",
    ];

    [Theory]
    [InlineData("OOH_ONLY", "Use only outdoor media in Johannesburg. Budget ZAR 10,000.", 1000000L)]
    [InlineData("FULL_CAMPAIGN", "Use radio and digital in Cape Town. Budget ZAR 25,000.", 2500000L)]
    [InlineData(null, "Please promote our launch", null)]
    public async Task SuppliedBriefUsesSourceBoundaryAndTargetedClarification(string? mode, string text, long? budget)
    {
        var input = new SuppliedBriefAgentInput(Guid.NewGuid(), Guid.NewGuid(), "Launch request", text, []);
        var client = CreateClient(async request =>
        {
            Assert.Equal("/v1/agents/brief_drafting", request.RequestUri!.AbsolutePath);
            var json = await request.Content!.ReadAsStringAsync();
            using var body = JsonDocument.Parse(json);
            var source = body.RootElement.GetProperty("source");
            Assert.Equal(text, source.GetProperty("source_content").GetString());
            var artifact = SuppliedArtifact(mode, text, budget, source.GetProperty("source_hash").GetString()!);
            var response = Response(JsonSerializer.SerializeToElement(artifact, AgentRuntimeHttpSupport.WireJson), []);
            var directory = Environment.GetEnvironmentVariable("ADVERTIFIED_TEST_EVIDENCE_DIRECTORY")
                ?? Path.Combine(Path.GetTempPath(), "advertified-contracts");
            Directory.CreateDirectory(directory);
            var name = mode ?? "AMBIGUOUS";
            await File.WriteAllTextAsync(Path.Combine(directory, $"brief-{name}-request.json"), json);
            await File.WriteAllTextAsync(Path.Combine(directory, $"brief-{name}-response.json"),
                await response.Content.ReadAsStringAsync());
            return response;
        });
        var result = await new HttpSuppliedBriefAgentClient(client, Settings()).UnderstandAsync(input, default);
        Assert.Equal(mode, result.CampaignMode);
        Assert.Equal(budget, result.Draft.BudgetMinor);
        Assert.Equal(mode is null, result.RequiresHumanClarification);
        Assert.Equal(text, Assert.Single(result.Evidence).Excerpt);
    }

    [Fact]
    public async Task SuppliedBriefProviderFailureNeverFallsBackToFixture()
    {
        var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("{}") }));
        var input = new SuppliedBriefAgentInput(Guid.NewGuid(), Guid.NewGuid(), "Launch", "Promote our launch", []);
        await Assert.ThrowsAsync<AgentRuntimeRejectedException>(() =>
            new HttpSuppliedBriefAgentClient(client, Settings()).UnderstandAsync(input, default));
    }

    [Fact]
    public async Task SuppliedBriefRetainsUsageWhenRuntimeRejectsAfterInference()
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
                    units = 1_500,
                    tool_calls = 0,
                    incremental_cost_minor = 1,
                    cache_status = "LIVE",
                    provider_request_id = "request-brief-123",
                    input_tokens = 1_000,
                    output_tokens = 500,
                    incremental_cost_usd_micros = 125,
                },
            },
        });
        var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(
            System.Net.HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(failure),
        }));
        var input = new SuppliedBriefAgentInput(
            Guid.NewGuid(), Guid.NewGuid(), "Launch", "Promote our launch", []);

        var rejected = await Assert.ThrowsAsync<SuppliedBriefValidationException>(() =>
            new HttpSuppliedBriefAgentClient(client, BedrockSettings())
                .UnderstandAsync(input, default));

        Assert.Equal("request-brief-123", rejected.Usage.ProviderRequestId);
        Assert.Equal(1, rejected.Usage.IncrementalCostMinor);
        Assert.Equal(125, rejected.Usage.IncrementalCostUsdMicros);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuppliedBriefRejectsChangedSourceOrInventedCitation(bool changedSource)
    {
        var client = CreateClient(async request =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var hash = body.RootElement.GetProperty("source").GetProperty("source_hash").GetString()!;
            var artifact = SuppliedArtifact(null, changedSource ? "Launch" : "Invented quotation", null,
                changedSource ? new string('0', 64) : hash);
            return Response(JsonSerializer.SerializeToElement(artifact, AgentRuntimeHttpSupport.WireJson), []);
        });
        var input = new SuppliedBriefAgentInput(Guid.NewGuid(), Guid.NewGuid(), "Launch", "Launch", []);
        var failure = await Assert.ThrowsAsync<SuppliedBriefValidationException>(() =>
            new HttpSuppliedBriefAgentClient(client, Settings()).UnderstandAsync(input, default));
        Assert.NotEmpty(failure.ResponseJson);
        Assert.NotNull(failure.Usage);
    }

    private static Microsoft.Extensions.Options.IOptions<AgentRuntimeOptions>
        BedrockSettings()
    {
        const string model = "amazon.nova-lite-v1:0";
        var routes = BedrockRoutes.ToDictionary(
            route => route, _ => model, StringComparer.Ordinal);
        return Microsoft.Extensions.Options.Options.Create(new AgentRuntimeOptions
        {
            Mode = AgentRuntimeOptions.HttpMode,
            BaseUrl = "http://agent-runtime.test",
            ServiceKey = "local-service-key",
            Provider = AgentRuntimeOptions.BedrockProvider,
            DefaultModel = model,
            DefaultCostCapMinor = 1,
            MaxAttempts = 1,
            AllowLive = true,
            Models = routes,
        });
    }

    private static object SuppliedArtifact(string? mode, string text, long? budget, string hash) => new
    {
        SourceHash = hash, ClientName = (string?)null, Title = "Launch request", CampaignMode = mode,
        CampaignModeConfidence = mode is null ? 0m : 1m, RequiresHumanClarification = mode is null,
        CampaignModeRationale = "Use only supplied scope.",
        Draft = new SuppliedBriefDraftView("", "", [], [], "", budget, budget is null,
            budget is null ? null : "ZAR", null, null, [], [], [], [], [], [], []),
        Questions = mode is null
            ? new[] { new SuppliedBriefQuestionView("campaignMode", "Which channels are permitted?", true, ["OOH_ONLY", "FULL_CAMPAIGN"]) }
            : [],
        Evidence = new[] { new SuppliedBriefEvidenceView("campaignMode", "SUPPLIED_CLAIM", text, 1m, "supplied:brief/current") },
    };
}
