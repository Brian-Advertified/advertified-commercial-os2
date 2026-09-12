using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task<object> ExerciseCoverageScenarioAsync(
        HttpClient client, string id, JsonElement shortlist, JsonElement[] candidates, long budget)
    {
        if (id == "PLAN-008")
        {
            var candidate = Assert.Single(candidates);
            var assessment = candidate.GetProperty("suitability").GetProperty("buyAssessment");
            var digital = assessment.GetProperty("digitalExposure");
            Assert.Equal(30, digital.GetProperty("spotLengthSeconds").GetInt32());
            Assert.Equal(15, digital.GetProperty("slotLengthSeconds").GetInt32());
            Assert.Empty(shortlist.GetProperty("campaignCombinations").GetProperty("alternatives").EnumerateArray());
            return new { candidate, incompatibleCreativeExcludedFromAlternatives = true };
        }
        var eligible = candidates.Where(item => item.GetProperty("isEligible").GetBoolean()).ToArray();
        JsonElement[] selected;
        if (id == "PLAN-002")
        {
            selected = MultiScenarioChannels.Select(channel =>
                Assert.Single(eligible, item => item.GetProperty("channel").GetString() == channel)).ToArray();
        }
        else
        {
            var requirements = eligible.SelectMany(item =>
                item.GetProperty("spatialMatch").GetProperty("requiredRequirementIds")
                    .EnumerateArray().Select(value => value.GetGuid())).Distinct().ToArray();
            Assert.Equal(2, requirements.Length);
            selected = requirements.Select(requirement => eligible.First(item =>
                item.GetProperty("spatialMatch").GetProperty("matchedRequiredRequirementIds")
                    .EnumerateArray().Any(value => value.GetGuid() == requirement))).Distinct().ToArray();
            Assert.Equal(2, selected.Length);
            Assert.All(selected, item => Assert.Single(
                item.GetProperty("spatialMatch").GetProperty("matchedRequiredRequirementIds").EnumerateArray()));
        }
        using var incomplete = await RawCommandAsync(client,
            Path($"shortlist-versions/{shortlist.GetProperty("id").GetGuid()}:select"), id + ":incomplete", 1,
            new { selectedCandidateIds = new[] { selected[0].GetProperty("id").GetGuid() },
                reason = "Incomplete required collective coverage must be rejected." });
        await AssertProblemAsync(incomplete, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        var plan = await ApproveScenarioPortfolioAsync(client, id, shortlist, selected, budget);
        return new { selected, plan, incompleteCoverageRejected = true };
    }

    private static async Task<JsonElement> ApproveScenarioPortfolioAsync(
        HttpClient client, string id, JsonElement shortlist, JsonElement[] selected, long budget)
    {
        using var selection = await CommandAsync(client,
            Path($"shortlist-versions/{shortlist.GetProperty("id").GetGuid()}:select"), id + ":select", 1,
            new { selectedCandidateIds = selected.Select(item => item.GetProperty("id").GetGuid()).ToArray(),
                reason = "Human selected the exact collectively covering portfolio." });
        using var draft = await CommandAsync(client, Path($"brief-versions/{BriefVersionId}/media-plans:generate"),
            id + ":plan", 1, new { });
        var plan = draft.RootElement;
        var lines = plan.GetProperty("lines").EnumerateArray().ToArray();
        Assert.Equal(selected.Length, lines.Length);
        Assert.Equal(selected.Select(item => item.GetProperty("productVersionId").GetGuid()).Order(),
            lines.Select(item => item.GetProperty("productVersionId").GetGuid()).Order());
        var cost = selected.Sum(item => item.GetProperty("rateAmountMinor").GetInt64());
        Assert.Equal(cost * 5 / 100, plan.GetProperty("feesMinor").GetInt64());
        Assert.Equal((cost + cost * 5 / 100) * 15 / 100, plan.GetProperty("vatMinor").GetInt64());
        Assert.Equal(cost + plan.GetProperty("feesMinor").GetInt64() + plan.GetProperty("vatMinor").GetInt64(),
            plan.GetProperty("totalMinor").GetInt64());
        Assert.InRange(plan.GetProperty("totalMinor").GetInt64(), 1, budget);
        var version = await ResolveScenarioPortfolioObjectionsAsync(client, id, plan);
        using var approved = await CommandAsync(client,
            Path($"media-plan-versions/{plan.GetProperty("id").GetGuid()}:approve"), id + ":plan-approve", version,
            new { reason = "Human reviewed collective coverage and exact canonical prices." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        return approved.RootElement.Clone();
    }

    private static async Task<long> ResolveScenarioPortfolioObjectionsAsync(
        HttpClient client, string id, JsonElement plan)
    {
        var planId = plan.GetProperty("id").GetGuid();
        var version = plan.GetProperty("version").GetInt64();
        var objections = plan.GetProperty("objections").EnumerateArray().ToArray();
        if (objections.Length == 0) return version;
        using var premature = await RawCommandAsync(client,
            Path($"media-plan-versions/{planId}:approve"), id + ":unresolved-approve", version,
            new { reason = "Unresolved material risks must block approval." });
        await AssertProblemAsync(premature, HttpStatusCode.Conflict, "PLANNING_APPROVAL_BLOCKED");
        foreach (var objection in objections)
        {
            var code = objection.GetProperty("code").GetString()!;
            Assert.True(code is "SUPPLY_UNCONFIRMED" or "BENCHMARK_INSUFFICIENT", code);
            using var resolved = await CommandAsync(client,
                Path($"media-plan-versions/{planId}/objections/{code}:resolve"), id + ":resolve:" + code,
                version, new { resolution = "ACCEPTED_WITH_REASON",
                    reason = "Human accepts the visible unconfirmed supply or insufficient peer evidence; no reach is assumed." });
            version = resolved.RootElement.GetProperty("version").GetInt64();
            Assert.Contains(resolved.RootElement.GetProperty("objections").EnumerateArray(),
                item => item.GetProperty("code").GetString() == code &&
                    item.GetProperty("resolution").GetString() == "ACCEPTED_WITH_REASON");
        }
        return version;
    }
}
