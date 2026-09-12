using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Planning;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class AgentRuntimeHttpAdapterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InventoryAdapterPreservesGovernedEligibilityAndRejectsForeignCandidates(bool foreignCandidate)
    {
        var input = InventoryWireInput();
        var client = CreateClient(async request =>
        {
            Assert.Equal("/v1/agents/inventory_intelligence", request.RequestUri!.AbsolutePath);
            var content = await request.Content!.ReadAsStringAsync();
            using var body = JsonDocument.Parse(content);
            var candidates = body.RootElement.GetProperty("inventory").GetProperty("candidates");
            Assert.True(candidates[0].GetProperty("is_eligible").GetBoolean());
            Assert.False(candidates[1].GetProperty("is_eligible").GetBoolean());
            Assert.Equal(JsonValueKind.Null, candidates[1].GetProperty("score").ValueKind);
            var directory = Environment.GetEnvironmentVariable("ADVERTIFIED_TEST_EVIDENCE_DIRECTORY");
            if (!foreignCandidate && !string.IsNullOrWhiteSpace(directory))
                await File.WriteAllTextAsync(Path.Combine(directory, "inventory.request.json"), content);
            return Response(new { interpretations = input.Candidates.Select((candidate, index) => new
            {
                candidate_id = foreignCandidate && index == 0 ? Guid.NewGuid() : candidate.CandidateId,
                rationale = "Explains supplied deterministic eligibility without changing it.",
                classification = MasterDataCodes.EvidenceClassifications.AiRecommendation,
            }).ToArray() }, []);
        });
        var adapter = new HttpInventoryIntelligenceAgentClient(client, Settings());
        if (foreignCandidate)
            await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.InterpretShortlistAsync(input, default));
        else
        {
            var result = await adapter.InterpretShortlistAsync(input, default);
            Assert.Equal(input.Candidates.Select(item => item.CandidateId),
                result.Interpretations.Select(item => item.CandidateId));
            Assert.Equal(0, result.Usage.IncrementalCostMinor);
        }
    }

    private static InventoryIntelligenceInput InventoryWireInput() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BriefVersionId, 3,
        Guid.NewGuid(), 1, [InventoryWireCandidate(true), InventoryWireCandidate(false)],
        new InventoryStrategyInput(Guid.NewGuid(), 2, Guid.NewGuid(), 2, Guid.NewGuid(), 3,
            "Increase qualified visits", null, null,
            [new(Guid.NewGuid(), "Client-supplied audience", null, null, ["Johannesburg"],
                MasterDataCodes.EvidenceClassifications.ClientRequirement, [], [])],
            [new("OOH", 500_000, "Approved awareness role",
                [new(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))])]), []);

    private static InventoryCandidateInput InventoryWireCandidate(bool eligible)
    {
        var score = eligible ? 0.8m : 0m;
        return new(Guid.NewGuid(), Guid.NewGuid(), "Synthetic inventory contract fixture", "OOH",
            "Johannesburg", 125_000, "ZAR", eligible,
            eligible ? null : MasterDataCodes.RejectionReasons.Unavailable,
            eligible ? null : "Supplier has not confirmed availability.", eligible ? score : null,
            new(null, null, null, ["No measured audience supplied."], null, null, null, null, null, [], []),
            new("SUITABILITY_V1", score, score, score, score, score, score, score, [], null), null);
    }
}
