using System.Text.Json;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Intelligence;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    private const string MarketExcerpt = "The retained sample recorded 17 enquiries in its stated period.";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarketAdapterRequiresApprovedContentRatherThanEvidenceIdentifiers(bool includeSnapshot)
    {
        var input = new MarketIntelligenceInput(AudienceInput().Problem,
            includeSnapshot ? [MarketEvidence(EvidenceId)] : []);
        var client = CreateClient(async request =>
        {
            Assert.Equal("/v1/agents/market_intelligence", request.RequestUri!.AbsolutePath);
            var content = await request.Content!.ReadAsStringAsync();
            using var json = JsonDocument.Parse(content);
            Assert.Equal(BriefVersionId, json.RootElement.GetProperty("market")
                .GetProperty("brief_version_id").GetGuid());
            var snapshots = json.RootElement.GetProperty("approved_evidence");
            Assert.Equal(includeSnapshot ? 1 : 0, snapshots.GetArrayLength());
            if (includeSnapshot)
            {
                Assert.Equal(MarketExcerpt, snapshots[0].GetProperty("excerpt").GetString());
                Assert.Equal(17, snapshots[0].GetProperty("structured_value").GetProperty("count").GetInt32());
                var directory = Environment.GetEnvironmentVariable("ADVERTIFIED_TEST_EVIDENCE_DIRECTORY");
                if (!string.IsNullOrWhiteSpace(directory))
                    await File.WriteAllTextAsync(Path.Combine(directory, "market.request.json"), content);
            }
            return Response(MarketArtifact([MarketFinding([EvidenceId])]), []);
        });
        var adapter = new HttpMarketIntelligenceAgentClient(client, Settings());
        if (!includeSnapshot)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.AnalyseAsync(input, default));
            return;
        }
        var result = await adapter.AnalyseAsync(input, default);
        Assert.Equal([EvidenceId], Assert.Single(result.Findings).EvidenceItemIds);
        Assert.Null(result.Findings[0].Confidence);
        Assert.Equal(0, result.Usage.IncrementalCostMinor);
    }

    [Fact]
    public async Task MarketAdapterRejectsOutOfScopeEvidenceBeforeMakingAProviderRequest()
    {
        var called = false;
        var client = CreateClient(_ =>
        {
            called = true;
            return Task.FromResult(Response(MarketArtifact([]), []));
        });
        var input = new MarketIntelligenceInput(AudienceInput().Problem,
            [MarketEvidence(Guid.Parse("99999999-9999-9999-9999-999999999999"))]);
        var adapter = new HttpMarketIntelligenceAgentClient(client, Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.AnalyseAsync(input, default));
        Assert.False(called);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarketAdapterRejectsUnevidencedFactsAndInventedHypothesisConfidence(bool hypothesis)
    {
        var finding = MarketFinding([]) with
        {
            Classification = hypothesis ? MasterDataCodes.EvidenceClassifications.Hypothesis
                : MasterDataCodes.EvidenceClassifications.Fact,
            Confidence = hypothesis ? 0.75m : null,
        };
        var client = CreateClient(_ => Task.FromResult(Response(MarketArtifact([finding]), [])));
        var adapter = new HttpMarketIntelligenceAgentClient(client, Settings());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.AnalyseAsync(new(AudienceInput().Problem, []), default));
    }

    [Fact]
    public async Task MarketAdapterAcceptsExplicitNoConclusionWithoutFabricatingOpportunities()
    {
        var client = CreateClient(_ => Task.FromResult(Response(MarketArtifact([]), [])));
        var adapter = new HttpMarketIntelligenceAgentClient(client, Settings());
        var result = await adapter.AnalyseAsync(new(AudienceInput().Problem, []), default);
        Assert.Empty(result.Findings);
        Assert.Empty(result.Opportunities);
        Assert.NotEmpty(result.EvidenceGaps);
    }

    private static AgentEvidenceInput MarketEvidence(Guid id) => new(id,
        MasterDataCodes.EvidenceClaimTypes.BusinessContext,
        JsonSerializer.SerializeToElement(new { count = 17, period = "source-specified period" }),
        MarketExcerpt);

    private static MarketFindingProposal MarketFinding(Guid[] evidenceIds) => new(
        "Observed sample", MarketExcerpt, "The recorded sample is not a population-wide demand estimate.",
        MasterDataCodes.EvidenceClassifications.Fact, evidenceIds, null);

    private static object MarketArtifact(MarketFindingProposal[] findings) => new
    {
        category_situation = "No verified market evidence beyond the supplied sample is available.",
        findings = findings.Select(item => new
        {
            title = item.Title, finding = item.Finding, commercial_implication = item.CommercialImplication,
            classification = item.Classification, evidence_item_ids = item.EvidenceItemIds,
            confidence = item.Confidence,
        }).ToArray(),
        opportunities = Array.Empty<object>(),
        strategic_implications = new[] { "Keep the source measurement limitations visible." },
        evidence_gaps = new[] { "Population demand and competitor behaviour are not established." },
    };
}
