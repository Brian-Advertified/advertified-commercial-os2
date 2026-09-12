using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    public static IEnumerable<object[]> BackendPlanningCases() =>
        BackendScenarioEvidence.Cases("PLANNING_OPTIMISATION");

    [Theory]
    [MemberData(nameof(BackendPlanningCases))]
    [Trait("Category", "Migration")]
    public Task CanonicalPlanningScenario(string scenarioId) =>
        BackendScenarioEvidence.RunAsync(scenarioId, () => scenarioId is "PLAN-014" or "PLAN-015"
            ? RunBudgetRevisionScenarioAsync(scenarioId) : RunPlanningScenarioAsync(scenarioId));

    private static async Task<BackendScenarioObservation> RunPlanningScenarioAsync(string id)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        var budget = id == "PLAN-013" ? 200_000L : 1_000_000L;
        await SeedAsync(connection, budget, includeInventory: id is not ("PLAN-002" or "PLAN-003" or "PLAN-005" or "PLAN-006" or "PLAN-008"));
        await SeedMissingSupplyScenarioAsync(connection, id);
        await SeedCoverageScenarioAsync(connection, id);
        await using var factory = CreateFactory(connection, OperatorId,
            configureServices: ConfigureDeterministicPlanningClock);
        await using var otherFactory = CreateFactory(connection, OtherUserId,
            configureServices: ConfigureDeterministicPlanningClock);
        using var client = factory.CreateClient();
        using var other = otherFactory.CreateClient();
        var shortlist = await PrepareScenarioShortlistAsync(client, id, budget);
        using var denied = await other.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        var observation = await ExercisePlanningScenarioAsync(client, id, shortlist, budget);
        using var workspaceResponse = await client.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        workspaceResponse.EnsureSuccessStatusCode();
        using var workspace = JsonDocument.Parse(await workspaceResponse.Content.ReadAsStringAsync());
        Assert.Equal(budget, workspace.RootElement.GetProperty("mediaMix").GetProperty("totalBudgetMinor").GetInt64());
        if (id == "PLAN-003")
        {
            Assert.Equal(JsonValueKind.Null, workspace.RootElement.GetProperty("shortlist").ValueKind);
            Assert.Equal(JsonValueKind.Null, workspace.RootElement.GetProperty("mediaPlan").ValueKind);
        }
        return new(new { approvedBriefVersionId = BriefVersionId, budgetMinor = budget,
                includeInventory = id != "PLAN-003", source = "Synthetic approved planning fixture" },
            new { shortlist, observation, workspace = workspace.RootElement.Clone(), crossTenantStatus = 403 },
            id is "PLAN-003" or "PLAN-004" or "PLAN-005" or "PLAN-006" or "PLAN-008" ? "HUMAN_REVIEW_REQUIRED" : "PLAN_APPROVED",
            new Dictionary<string, bool>
            {
                ["CANONICAL_ELIGIBILITY_USED"] = true,
                ["CANONICAL_CLIENT_PRICE_USED"] = true,
                ["NO_ASSUMED_REACH"] = true,
            }, ["AUDIENCE_APPROVAL", "MEDIA_STRATEGY_APPROVAL", "MIX_APPROVAL", "INVENTORY_SELECTION", "PLAN_APPROVAL"],
            0, null, "EXACT_POLICY_PRICE_AND_CHANNEL_BUDGET_CHECKED", "CROSS_TENANT_READ_REJECTED");
    }

    private static async Task<object> ExercisePlanningScenarioAsync(
        HttpClient client, string id, JsonElement shortlist, long budget)
    {
        if (id == "PLAN-003")
        {
            Assert.Equal("INVALID_LIFECYCLE_TRANSITION", shortlist.GetProperty("code").GetString());
            using var blocked = await RawCommandAsync(client, Path($"brief-versions/{BriefVersionId}/media-plans:generate"),
                id + ":no-inventory-plan", 1, new { });
            await AssertProblemAsync(blocked, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
            return new { rejection = "INVALID_LIFECYCLE_TRANSITION", selectedCount = 0 };
        }
        var candidates = shortlist.GetProperty("candidates").EnumerateArray().ToArray();
        AssertScenarioReachEvidence(candidates);
        var combinations = shortlist.GetProperty("campaignCombinations");
        Assert.True(combinations.GetProperty("clientPriceAssessed").GetBoolean());
        Assert.All(combinations.GetProperty("alternatives").EnumerateArray(), alternative =>
            Assert.InRange(alternative.GetProperty("campaignClientPriceMinor").GetInt64(), 1, budget));
        if (id is "PLAN-002" or "PLAN-007" or "PLAN-008")
            return await ExerciseCoverageScenarioAsync(client, id, shortlist, candidates, budget);
        if (id is "PLAN-005" or "PLAN-006")
            return await AssertMissingSupplyScenarioAsync(client, id, shortlist, candidates);
        if (id == "PLAN-004") return await AssertStaleScenarioSelectionAsync(client, id, shortlist, candidates);
        var eligible = candidates.Where(item => item.GetProperty("isEligible").GetBoolean()).ToArray();
        Assert.NotEmpty(eligible);
        var chosen = SelectScenarioCandidate(id, eligible);
        var plan = await ApproveScenarioPlanAsync(client, id, shortlist, chosen, budget);
        if (id == "PLAN-012")
            return await AssertScenarioSubstitutionAsync(client, id, chosen, plan, budget);
        return new { chosen, plan };
    }
}
