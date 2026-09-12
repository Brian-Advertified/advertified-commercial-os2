using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static void AssertScenarioReachEvidence(JsonElement[] candidates)
    {
        var measuredProduct = Guid.Parse("77000000-0000-0000-0000-000000000001");
        foreach (var item in candidates.Where(candidate =>
            candidate.GetProperty("inventoryProductId").GetGuid() != measuredProduct))
        {
            Assert.Empty(item.GetProperty("audienceFit").GetProperty("deliveryMeasurements").EnumerateArray());
            if (item.TryGetProperty("suitability", out var suitability) &&
                suitability.ValueKind == JsonValueKind.Object &&
                suitability.TryGetProperty("buyAssessment", out var assessment) &&
                assessment.ValueKind == JsonValueKind.Object)
                Assert.Equal(JsonValueKind.Null, assessment.GetProperty("reach").ValueKind);
        }
    }

    private static JsonElement SelectScenarioCandidate(string id, JsonElement[] eligible)
    {
        var measured = eligible.Where(item =>
            item.GetProperty("audienceFit").GetProperty("deliveryMeasurements").GetArrayLength() > 0).ToArray();
        var unknown = eligible.Where(item =>
            item.GetProperty("audienceFit").GetProperty("deliveryMeasurements").GetArrayLength() == 0).ToArray();
        Assert.NotEmpty(measured);
        Assert.NotEmpty(unknown);
        Assert.All(unknown, item => Assert.Empty(
            item.GetProperty("audienceFit").GetProperty("deliveryMeasurements").EnumerateArray()));
        if (id == "PLAN-011") return unknown.OrderBy(item => item.GetProperty("rateAmountMinor").GetInt64()).First();
        if (id == "PLAN-010")
        {
            var chosen = measured.OrderByDescending(item =>
                item.GetProperty("audienceFit").GetProperty("deliveryMeasurements")[0].GetProperty("value").GetDecimal()).First();
            Assert.Equal(125_000m, chosen.GetProperty("audienceFit")
                .GetProperty("deliveryMeasurements")[0].GetProperty("value").GetDecimal());
            return chosen;
        }
        var cheapest = eligible.OrderBy(item => item.GetProperty("rateAmountMinor").GetInt64()).First();
        Assert.Contains(eligible, item => item.GetProperty("rateAmountMinor").GetInt64() >
            cheapest.GetProperty("rateAmountMinor").GetInt64());
        return cheapest;
    }

    private static async Task<object> AssertStaleScenarioSelectionAsync(
        HttpClient client, string id, JsonElement shortlist, JsonElement[] candidates)
    {
        var stale = Assert.Single(candidates, item =>
            item.GetProperty("rejectionReason").GetString() == "STALE_RATE");
        Assert.False(stale.GetProperty("isEligible").GetBoolean());
        using var rejected = await RawCommandAsync(client,
            Path($"shortlist-versions/{shortlist.GetProperty("id").GetGuid()}:select"), id + ":stale-select", 1,
            new { selectedCandidateIds = new[] { stale.GetProperty("id").GetGuid() }, reason = "Must reject stale rate." });
        await AssertProblemAsync(rejected, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        return new { stale, rejection = "INVALID_LIFECYCLE_TRANSITION" };
    }

    private static async Task<object> AssertScenarioSubstitutionAsync(
        HttpClient client, string id, JsonElement previousCandidate, JsonElement previousPlan, long budget)
    {
        var shortlist = await NewScenarioShortlistAsync(client, id + ":replacement");
        var previousProduct = previousCandidate.GetProperty("inventoryProductId").GetGuid();
        var chosen = shortlist.GetProperty("candidates").EnumerateArray().First(item =>
            item.GetProperty("isEligible").GetBoolean() &&
            item.GetProperty("inventoryProductId").GetGuid() != previousProduct);
        var replacement = await ApproveScenarioPlanAsync(client, id + ":replacement", shortlist, chosen, budget);
        var previousPlanId = previousPlan.GetProperty("id").GetGuid();
        await AssertSupersededPlanCannotGenerateProposalAsync(client, previousPlanId, expectNoCurrentPlan: false);
        using var retainedResponse = await client.GetAsync(Path($"media-plans/{previousPlanId}"));
        retainedResponse.EnsureSuccessStatusCode();
        using var retained = JsonDocument.Parse(await retainedResponse.Content.ReadAsStringAsync());
        Assert.Equal(previousPlan.GetRawText(), retained.RootElement.GetRawText());
        Assert.NotEqual(previousProduct, replacement.GetProperty("lines")[0].GetProperty("inventoryProductId").GetGuid());
        return new { previousPlan, replacement, staleProposalRejected = true, historicalPlanUnchanged = true };
    }
}
