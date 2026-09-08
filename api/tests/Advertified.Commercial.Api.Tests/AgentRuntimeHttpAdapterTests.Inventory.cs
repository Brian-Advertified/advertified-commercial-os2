using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    [Fact]
    public async Task InventoryWireEvidenceContainsEligibleAndRejectedSuitability()
    {
        var input = InventoryInput(Guid.NewGuid(), Guid.NewGuid());
        input = input with { Strategy = new InventoryStrategyInput(
            Guid.NewGuid(), 2, Guid.NewGuid(), 3,
            "Increase pharmacy visits", "Reach caregivers", "Convenient family care",
            [new InventoryStrategyAudienceInput(Guid.NewGuid(), "Stay-at-home mothers",
                "Convenient care", "Purchase occasion requires research", ["Johannesburg"],
                "HYPOTHESIS", ["Do not infer household income"], [])],
            [new InventoryStrategyAllocationInput("OOH", 125_000, "Local awareness",
                [new MediaRunningPeriodView(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))])]) };
        var accepted = input.Candidates[0];
        var rejected = accepted with
        {
            CandidateId = Guid.NewGuid(), IsEligible = false, Score = null,
            RejectionReason = "CHANNEL_NOT_ALLOWED", RejectionDetail = "Channel is outside this brief.",
            Suitability = new InventorySuitabilityView("INVENTORY_SUITABILITY_OOH_V1",
                0, 0, 0, 0, 0, 0, 0, ["Channel is outside this brief."]),
        };
        input = input with { Candidates = [accepted, rejected] };
        var client = CreateClient(async request =>
        {
            var json = await request.Content!.ReadAsStringAsync();
            using var body = JsonDocument.Parse(json);
            var candidates = body.RootElement.GetProperty("inventory").GetProperty("candidates");
            var strategy = body.RootElement.GetProperty("inventory").GetProperty("strategy");
            Assert.Equal("HYPOTHESIS", strategy.GetProperty("audiences")[0].GetProperty("classification").GetString());
            Assert.Equal("2026-09-01", strategy.GetProperty("allocations")[0]
                .GetProperty("running_periods")[0].GetProperty("start").GetString());
            var references = body.RootElement.GetProperty("invocation").GetProperty("resource_refs");
            Assert.Equal(input.Strategy!.AudienceSetId, references[2].GetProperty("resource_id").GetGuid());
            Assert.Equal(2, references[2].GetProperty("version").GetInt64());
            Assert.Equal(0.82m, candidates[0].GetProperty("suitability").GetProperty("total").GetDecimal());
            Assert.Equal(0, candidates[1].GetProperty("suitability").GetProperty("total").GetDecimal());
            var directory = Path.Combine(Path.GetTempPath(), "advertified-contracts");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "inventory-shortlist.json"), json);
            return Response(new { interpretations = input.Candidates.Select(candidate => new
            {
                candidate_id = candidate.CandidateId, rationale = "Explanation preserves canonical eligibility.",
            }).ToArray() }, [EvidenceId]);
        });
        await new HttpPlanningAgentClient(client, Settings()).InterpretInventoryAsync(input, default);
    }

    [Fact]
    public async Task InventoryAdapterRejectsMismatchedScoreBeforeHttp()
    {
        var input = InventoryInput(Guid.NewGuid(), Guid.NewGuid());
        input = input with { Candidates = [input.Candidates[0] with { Score = 0.1m }] };
        var client = CreateClient(_ => throw new InvalidOperationException("HTTP must not be used."));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new HttpPlanningAgentClient(client, Settings()).InterpretInventoryAsync(input, default));
    }

    [Fact]
    public async Task InventoryAdapterUsesExactShortlistAndPreservesCandidateSet()
    {
        var candidateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shortlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var client = CreateClient(async request =>
        {
            Assert.Equal(
                "/v1/agents/inventory_intelligence",
                request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(
                await request.Content!.ReadAsStreamAsync(CancellationToken.None));
            var references = body.RootElement.GetProperty("invocation")
                .GetProperty("resource_refs");
            Assert.Equal(BriefVersionId, references[0].GetProperty("resource_id").GetGuid());
            Assert.Equal(shortlistId, references[1].GetProperty("resource_id").GetGuid());
            var candidate = body.RootElement.GetProperty("inventory")
                .GetProperty("candidates")[0];
            Assert.True(candidate.GetProperty("is_eligible").GetBoolean());
            Assert.Equal(4, candidate.GetProperty("benchmark")
                .GetProperty("cohort_size").GetInt32());
            return Response(
                new
                {
                    interpretations = new[]
                    {
                        new
                        {
                            candidate_id = candidateId,
                            rationale = "Eligible after governed hard constraints.",
                        },
                    },
                },
                [EvidenceId]);
        });
        var adapter = new HttpPlanningAgentClient(client, Settings());

        var result = await adapter.InterpretInventoryAsync(
            InventoryInput(candidateId, shortlistId),
            CancellationToken.None);

        var interpretation = Assert.Single(result.Interpretations);
        Assert.Equal(candidateId, interpretation.CandidateId);
        Assert.Equal(
            "Eligible after governed hard constraints.",
            interpretation.Rationale);
        Assert.Equal(0, result.IncrementalCostMinor);
    }

    [Fact]
    public async Task InventoryAdapterRejectsChangedCandidateIdentity()
    {
        var candidateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var shortlistId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var client = CreateClient(request => Task.FromResult(Response(
            new
            {
                interpretations = new[]
                {
                    new
                    {
                        candidate_id = Guid.Parse(
                            "cccccccc-cccc-cccc-cccc-cccccccccccc"),
                        rationale = "This candidate was not supplied.",
                    },
                },
            },
            [EvidenceId])));
        var adapter = new HttpPlanningAgentClient(client, Settings());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.InterpretInventoryAsync(
                InventoryInput(candidateId, shortlistId),
                CancellationToken.None));
    }

    private static InventoryIntelligenceInput InventoryInput(
        Guid candidateId,
        Guid shortlistId) => new(
            BriefInput(),
            shortlistId,
            1,
            [
                new InventoryIntelligenceCandidateInput(
                    candidateId,
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    "Bree Street Gantry",
                    "OOH",
                    "Johannesburg",
                    125_000,
                    "ZAR",
                    true,
                    null,
                    null,
                    0.82m,
                    new InventoryAudienceFitView(0.8m, null, null, []),
                    new InventorySuitabilityView("INVENTORY_SUITABILITY_OOH_V1",
                        1m, 0.8m, 0.8m, 0.8m, 0.5m, 0.6m, 0.82m, []),
                    new InventoryBenchmarkInput(
                        "OOH_LOCAL_PEER_V1",
                        "RADIUS_5_KM",
                        4,
                        166_667,
                        25,
                        "STRONG_VALUE",
                        0.70m,
                        [])),
            ]);
}
