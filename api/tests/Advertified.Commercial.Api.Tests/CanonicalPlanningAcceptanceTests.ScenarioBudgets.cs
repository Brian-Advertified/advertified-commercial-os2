using System.Net;
using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static readonly JsonSerializerOptions ScenarioBriefJson = new(JsonSerializerDefaults.Web);

    private static async Task<BackendScenarioObservation> RunBudgetRevisionScenarioAsync(string id)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        const long originalBudget = 200_000;
        var revisedBudget = id == "PLAN-014" ? 350_000L : 150_000L;
        await SeedAsync(connection, originalBudget);
        await using var factory = CreateFactory(connection, OperatorId,
            configureServices: ConfigureDeterministicPlanningClock);
        await using var otherFactory = CreateFactory(connection, OtherUserId,
            configureServices: ConfigureDeterministicPlanningClock);
        using var client = factory.CreateClient();
        using var other = otherFactory.CreateClient();
        var first = await PrepareScenarioShortlistAsync(client, id + ":original", originalBudget);
        var previousPlan = await ApproveScenarioPlanAsync(client, id + ":original", first,
            SelectScenarioCandidate(id, first.GetProperty("candidates").EnumerateArray()
                .Where(item => item.GetProperty("isEligible").GetBoolean()).ToArray()), originalBudget);
        var revision = await ApproveBudgetRevisionAsync(client, id, revisedBudget);
        var shortlist = await PrepareScenarioShortlistAsync(client, id + ":revised", revisedBudget, revision.Id);
        var candidates = shortlist.GetProperty("candidates").EnumerateArray().ToArray();
        AssertScenarioReachEvidence(candidates);
        var plan = await ApproveScenarioPlanAsync(client, id + ":revised", shortlist,
            SelectScenarioCandidate(id, candidates.Where(item => item.GetProperty("isEligible").GetBoolean()).ToArray()),
            revisedBudget);
        using var previousResponse = await client.GetAsync(Path($"media-plans/{previousPlan.GetProperty("id").GetGuid()}"));
        previousResponse.EnsureSuccessStatusCode();
        using var previous = JsonDocument.Parse(await previousResponse.Content.ReadAsStringAsync());
        Assert.Equal(previousPlan.GetRawText(), previous.RootElement.GetRawText());
        Assert.Equal(revision.Id, plan.GetProperty("briefVersionId").GetGuid());
        using var denied = await other.GetAsync(Path($"brief-versions/{revision.Id}/planning"));
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        return new(new { originalBudget, revisedBudget, originalBriefVersionId = BriefVersionId },
            new { previousPlan, revision, shortlist, plan, crossTenantStatus = 403 },
            "REVISED_BUDGET_PLAN_APPROVED", new Dictionary<string, bool>
            {
                ["CANONICAL_ELIGIBILITY_USED"] = true,
                ["CANONICAL_CLIENT_PRICE_USED"] = true,
                ["NO_ASSUMED_REACH"] = true,
            }, ["BUDGET_REVISION_APPROVAL", "AUDIENCE_APPROVAL", "MIX_APPROVAL", "INVENTORY_SELECTION", "PLAN_APPROVAL"],
            0, null, "NEW_BRIEF_VERSION_AND_PLAN_RECONCILED_WITH_HISTORICAL_PLAN_RETAINED",
            "CROSS_TENANT_READ_REJECTED");
    }

    private static async Task<BriefVersionView> ApproveBudgetRevisionAsync(HttpClient client, string id, long budget)
    {
        using var response = await client.GetAsync(Path($"briefs/{BriefId}"));
        response.EnsureSuccessStatusCode();
        using var detail = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var source = detail.RootElement.GetProperty("versions").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == BriefVersionId)
            .Deserialize<BriefVersionView>(ScenarioBriefJson)!;
        var command = new CreateBriefVersionCommand(
            BriefId, source.Id, source.BusinessProblem, source.Objective, source.Audiences, source.Geographies,
            source.Timing, budget, false, source.Currency, source.VatStatus, source.FeesMinor,
            source.MediaRequirements, source.Constraints, source.Measurement, source.Facts, source.Unknowns,
            source.Assumptions, source.Conflicts, source.EvidenceItemIds, AudienceResearch: source.AudienceResearch);
        using var draft = await CommandAsync(client, Path($"briefs/{BriefId}/versions"), id + ":brief-revision", 1, command);
        var draftId = draft.RootElement.GetProperty("id").GetGuid();
        using var ready = await CommandAsync(client, Path($"brief-versions/{draftId}:ready"), id + ":brief-ready",
            draft.RootElement.GetProperty("version").GetInt64(), new MarkBriefVersionReadyCommand());
        using var submitted = await CommandAsync(client, Path($"brief-versions/{draftId}:submit"), id + ":brief-submit",
            ready.RootElement.GetProperty("version").GetInt64(),
            new SubmitBriefVersionCommand(OperatorId, "Assign the policy-authorised human confirmation."));
        using var approved = await CommandAsync(client, Path($"brief-versions/{draftId}:approve"), id + ":brief-approve",
            submitted.RootElement.GetProperty("version").GetInt64(),
            new ApproveBriefVersionCommand("Human explicitly confirms the revised synthetic budget."));
        var revision = approved.RootElement.Deserialize<BriefVersionView>(ScenarioBriefJson)!;
        Assert.Equal("APPROVED", revision.Status);
        Assert.Equal(budget, revision.BudgetMinor);
        Assert.Equal(BriefVersionId, revision.BaseVersionId);
        Assert.NotEqual(source.Id, revision.Id);
        return revision;
    }
}
