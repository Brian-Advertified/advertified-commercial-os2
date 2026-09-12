using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task<JsonElement> PrepareScenarioShortlistAsync(HttpClient client, string id, long budget, Guid? versionId = null)
    {
        var currentBrief = versionId ?? BriefVersionId;
        using var mode = await CommandAsync(client,
            Path($"brief-versions/{currentBrief}/campaign-mode:select"), id + ":mode", 1, Mode(id == "PLAN-002" ? "FULL_CAMPAIGN" : "OOH_ONLY"));
        using var audience = await CommandAsync(client,
            Path($"brief-versions/{currentBrief}/audiences:generate"), id + ":audience", 1, new { });
        var audienceId = audience.RootElement.GetProperty("id").GetGuid();
        using var approvedAudience = await CommandAsync(client,
            Path($"audience-strategies/{audienceId}:approve"), id + ":audience-approve", 1, new
            {
                targetAudienceIds = audience.RootElement.GetProperty("targetAudienceIds")
                    .EnumerateArray().Select(item => item.GetGuid()).ToArray(),
                targetingRationale = audience.RootElement.GetProperty("targetingRationale").GetString(),
                positioningStatement = audience.RootElement.GetProperty("positioningStatement").GetString(),
                reason = "Synthetic scenario: human reviewed exact supplied audience.",
            });
        using var strategy = await AnalyseAndApproveMediaStrategyAsync(client, currentBrief);
        using var mix = await CommandAsync(client, Path($"brief-versions/{currentBrief}/media-mixes:generate"),
            id + ":mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var edited = await CommandAsync(client, Path($"media-mix-versions/{mixId}:update"),
            id + ":mix-edit", 1, new
            {
                allocations = ScenarioAllocations(id, budget),
                reason = "Use the approved scenario budget and period.",
            });
        using var approved = await CommandAsync(client, Path($"media-mix-versions/{mixId}:approve"),
            id + ":mix-approve", 2, new { reason = "Human confirms the exact media mix." });
        if (id == "PLAN-003")
        {
            using var blocked = await RawCommandAsync(client, Path($"brief-versions/{currentBrief}/shortlists:generate"),
                id + ":shortlist", 1, new { });
            await AssertProblemAsync(blocked, System.Net.HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
            using var problem = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
            return problem.RootElement.Clone();
        }
        return await NewScenarioShortlistAsync(client, id, currentBrief);
    }

    private static async Task<JsonElement> NewScenarioShortlistAsync(HttpClient client, string key, Guid? versionId = null)
    {
        var currentBrief = versionId ?? BriefVersionId;
        using var shortlist = await CommandAsync(client, Path($"brief-versions/{currentBrief}/shortlists:generate"),
            key + ":shortlist", 1, new { });
        return shortlist.RootElement.Clone();
    }

    private static async Task<JsonElement> ApproveScenarioPlanAsync(
        HttpClient client, string key, JsonElement shortlist, JsonElement candidate, long budget)
    {
        using var selected = await CommandAsync(client,
            Path($"shortlist-versions/{shortlist.GetProperty("id").GetGuid()}:select"), key + ":select", 1, new
            {
                selectedCandidateIds = new[] { candidate.GetProperty("id").GetGuid() },
                reason = "Human selected this exact current eligible candidate.",
            });
        var currentBrief = shortlist.GetProperty("briefVersionId").GetGuid();
        using var draft = await CommandAsync(client, Path($"brief-versions/{currentBrief}/media-plans:generate"),
            key + ":plan", 1, new { });
        var plan = draft.RootElement;
        Assert.InRange(plan.GetProperty("totalMinor").GetInt64(), 1, budget);
        var line = Assert.Single(plan.GetProperty("lines").EnumerateArray());
        Assert.Equal(candidate.GetProperty("productVersionId").GetGuid(), line.GetProperty("productVersionId").GetGuid());
        var rate = candidate.GetProperty("rateAmountMinor").GetInt64();
        // Fixture policy is 5% fee, then 15% VAT; September is one monthly buying period.
        Assert.Equal(rate * 5 / 100, plan.GetProperty("feesMinor").GetInt64());
        Assert.Equal((rate + rate * 5 / 100) * 15 / 100, plan.GetProperty("vatMinor").GetInt64());
        Assert.Equal(rate + plan.GetProperty("feesMinor").GetInt64() + plan.GetProperty("vatMinor").GetInt64(),
            plan.GetProperty("totalMinor").GetInt64());
        using var approved = await CommandAsync(client,
            Path($"media-plan-versions/{plan.GetProperty("id").GetGuid()}:approve"), key + ":approve-plan", 1,
            new { reason = "Human reviewed exact inventory, commercial price and limitations." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        Assert.Equal(OperatorId, approved.RootElement.GetProperty("approvedBy").GetGuid());
        return approved.RootElement.Clone();
    }
}
