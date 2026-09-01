using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Opportunity;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
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
