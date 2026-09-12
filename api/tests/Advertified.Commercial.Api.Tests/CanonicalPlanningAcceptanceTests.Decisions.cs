using System.Net;
using System.Text.Json;
using Advertified.Commercial.Domain.MasterData;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task AssertReplacementDecisionHistoryAsync(
        HttpClient client, HttpClient other, string connectionString, Guid originalProductId, Guid originalPlanId)
    {
        var path = Path($"reporting/inventory-decisions?briefVersionId={BriefVersionId}");
        using var deniedClient = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, deniedClient.StatusCode);
        using var deniedTenant = await other.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, deniedTenant.StatusCode);
        await SetOperatorRoleAsync(connectionString, MasterDataCodes.Roles.AgencyAdmin);
        var olderDraft = await CreateOlderSelectionDraftAsync(client);
        using var shortlist = await CommandAsync(client,
            Path($"brief-versions/{BriefVersionId}/shortlists:generate"), "decision-successor", 1, new { });
        var successorId = shortlist.RootElement.GetProperty("id").GetGuid();
        var candidate = shortlist.RootElement.GetProperty("candidates").EnumerateArray().First(item =>
            item.GetProperty("isEligible").GetBoolean() &&
            item.GetProperty("inventoryProductId").GetGuid() != originalProductId);
        using var selected = await CommandAsync(client,
            Path($"shortlist-versions/{successorId}:select"), "decision-replace", 1, new
            {
                selectedCandidateIds = new[] { candidate.GetProperty("id").GetGuid() },
                reason = "Replaced the original placement to improve local coverage; private client context.",
            });
        await AssertOlderSelectionDraftRejectedAsync(client, olderDraft);
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var removed = report.RootElement.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("productId").GetGuid() == originalProductId &&
            item.GetProperty("shortlistVersionId").GetGuid() == successorId);
        Assert.False(removed.GetProperty("isSelected").GetBoolean());
        Assert.True(removed.GetProperty("presentInCurrentShortlist").GetBoolean());
        Assert.True(removed.GetProperty("wasSelected").GetBoolean());
        Assert.NotEqual(Guid.Empty, removed.GetProperty("previousEventId").GetGuid());
        Assert.Equal(OperatorId, removed.GetProperty("actorId").GetGuid());
        Assert.Contains("private client context", removed.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.False(report.RootElement.GetProperty("supplierSafe").GetBoolean());
        await AssertSupersededPlanCannotGenerateProposalAsync(client, originalPlanId);
        await AssertIndependentMixHasNoPreviousSelectionAsync(client, originalProductId);
        await AssertIneligibleRemovalIsStillPresentAsync(client, connectionString, originalProductId);
    }

    private static async Task AssertSupersededPlanCannotGenerateProposalAsync(
        HttpClient client, Guid originalPlanId, bool expectNoCurrentPlan = true)
    {
        using var workspaceResponse = await client.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        workspaceResponse.EnsureSuccessStatusCode();
        using var workspace = JsonDocument.Parse(await workspaceResponse.Content.ReadAsStringAsync());
        if (expectNoCurrentPlan)
            Assert.Equal(JsonValueKind.Null, workspace.RootElement.GetProperty("mediaPlan").ValueKind);
        using var proposal = await RawCommandAsync(client,
            Path($"briefs/{BriefId}/proposals:generate"), "decision-stale-proposal", 1, new
            {
                title = "Must not reuse the removed selection",
                options = Enumerable.Range(1, 3).Select(index => new
                {
                    planVersionId = originalPlanId, label = $"Option {index}", outcome = "Stale selection",
                }).ToArray(),
                terms = "Synthetic stale-selection regression.", expiryAtUtc = Now.AddDays(7),
            });
        await AssertProblemAsync(proposal, HttpStatusCode.Conflict, "PROPOSAL_STALE");
    }

    private static async Task AssertIndependentMixHasNoPreviousSelectionAsync(HttpClient client, Guid productId)
    {
        using var shortlist = await CreateIndependentMixShortlistAsync(client);
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        var candidateId = shortlist.RootElement.GetProperty("candidates").EnumerateArray()
            .Single(item => item.GetProperty("inventoryProductId").GetGuid() == productId)
            .GetProperty("id").GetGuid();
        using var selected = await CommandAsync(client, Path($"shortlist-versions/{shortlistId}:select"),
            "decision-independent-select", 1, new
            {
                selectedCandidateIds = new[] { candidateId }, reason = "Independent campaign scenario, not a replacement.",
            });
        using var response = await client.GetAsync(Path($"reporting/inventory-decisions?briefVersionId={BriefVersionId}"));
        response.EnsureSuccessStatusCode();
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var scenario = report.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("shortlistVersionId").GetGuid() == shortlistId).ToArray();
        Assert.NotEmpty(scenario);
        Assert.All(scenario, item =>
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("previousEventId").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("wasSelected").ValueKind);
        });
    }

    private static async Task<JsonDocument> CreateIndependentMixShortlistAsync(HttpClient client)
    {
        using var response = await client.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        response.EnsureSuccessStatusCode();
        using var workspace = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var allocations = workspace.RootElement.GetProperty("mediaMix").GetProperty("allocations");
        using var mix = await CommandAsync(client, Path($"brief-versions/{BriefVersionId}/media-mixes:generate"),
            "decision-independent-mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var edited = await CommandAsync(client, Path($"media-mix-versions/{mixId}:update"),
            "decision-independent-edit", 1, new { allocations, reason = "Independent comparable scenario." });
        using var approved = await CommandAsync(client, Path($"media-mix-versions/{mixId}:approve"),
            "decision-independent-approve", 2, new { reason = "Reviewed independent scenario." });
        return await CommandAsync(client, Path($"brief-versions/{BriefVersionId}/shortlists:generate"),
            "decision-independent-shortlist", 1, new { });
    }
}
