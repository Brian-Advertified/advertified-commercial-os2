using System.Net;
using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Domain.MasterData;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    private static async Task AssertMarketEvidenceJourneyAsync(
        HttpClient owner, HttpClient advertiser, Guid briefVersionId)
    {
        var path = $"/api/v1/tenants/{TenantId}/brief-versions/{briefVersionId}/intelligence/market";
        using var generated = await owner.PostAsync(path, null);
        var body = await generated.Content.ReadAsStringAsync();
        Assert.True(generated.IsSuccessStatusCode, body);
        using var created = JsonDocument.Parse(body);
        Assert.Equal(briefVersionId, created.RootElement.GetProperty("subjectId").GetGuid());
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Draft,
            created.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, created.RootElement.GetProperty("totalIncrementalCostMinor").GetInt64());
        using var artifact = JsonDocument.Parse(created.RootElement.GetProperty("artifactJson").GetString()!);
        Assert.Single(artifact.RootElement.GetProperty("findings").EnumerateArray());
        Assert.NotEmpty(artifact.RootElement.GetProperty("findings")[0]
            .GetProperty("evidenceItemIds").EnumerateArray());
        using var latestResponse = await owner.GetAsync(path);
        Assert.True(latestResponse.IsSuccessStatusCode, await latestResponse.Content.ReadAsStringAsync());
        using var latest = await ReadJsonAsync(latestResponse);
        Assert.Equal(created.RootElement.GetProperty("id").GetGuid(),
            latest.RootElement.GetProperty("id").GetGuid());
        using var denied = await advertiser.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var crossTenant = await owner.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/brief-versions/{briefVersionId}/intelligence/market");
        Assert.Equal(HttpStatusCode.Forbidden, crossTenant.StatusCode);
    }

    private sealed class RetainedMarketEvidenceFixture : IMarketIntelligenceAgentClient
    {
        public Task<MarketIntelligenceAgentProposal> AnalyseAsync(
            MarketIntelligenceInput input, CancellationToken cancellationToken)
        {
            Assert.NotEmpty(input.ApprovedEvidence);
            Assert.Equal(input.Problem.EvidenceItemIds.Order(), input.ApprovedEvidence.Select(item => item.Id).Order());
            Assert.All(input.ApprovedEvidence, item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Excerpt));
                Assert.Equal(JsonValueKind.Object, item.StructuredValue.ValueKind);
            });
            var evidence = input.ApprovedEvidence[0];
            return Task.FromResult(new MarketIntelligenceAgentProposal(
                "Only the retained reviewed source is used in this deterministic boundary test.",
                [new MarketFindingProposal("Retained source context", evidence.Excerpt,
                    "This source context does not establish population-wide market demand.",
                    MasterDataCodes.EvidenceClassifications.Inference, [evidence.Id], null)],
                [], ["Review the source limits before making a commercial recommendation."],
                ["External market demand is not established."], [], [],
                "Synthetic test adapter copied approved content; no external analysis was performed.",
                new IntelligenceInvocationUsage(MasterDataCodes.AgentTypes.MarketIntelligence,
                    "deterministic", "fixture-v1", 0, "FIXTURE", null, 0, 0, 0)));
        }
    }
}
