using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task SoloAgencyOperatorTakesApprovedBriefThroughApprovedPlan()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var operatorFactory = CreateFactory(connectionString, OperatorId);
        await using var otherFactory = CreateFactory(connectionString, OtherUserId);
        using var client = operatorFactory.CreateClient();
        using var other = otherFactory.CreateClient();

        using var campaignMode = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/campaign-mode:select"),
            "planning-mode-ooh", 1, new
            {
                mode = "OOH_ONLY",
                decisionSource = "HUMAN_CLARIFICATION",
                confidence = 1m,
                reason = "The approved Brief requires out-of-home media only.",
            });
        Assert.Equal("OOH_ONLY", campaignMode.RootElement.GetProperty("mode").GetString());
        Assert.True(campaignMode.RootElement.GetProperty("isLocked").GetBoolean());
        using var attemptedExpansion = await RawCommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/campaign-mode:select"),
            "planning-mode-expand", 1, new
            {
                mode = "FULL_CAMPAIGN",
                decisionSource = "HUMAN_CLARIFICATION",
                confidence = 1m,
                reason = "This must require a new campaign.",
            });
        await AssertProblemAsync(
            attemptedExpansion, HttpStatusCode.Conflict, "CAMPAIGN_MODE_LOCKED");

        using var audience = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/audiences:generate"),
            "planning-audience", 1, new { });
        Assert.Equal("APPROVED", audience.RootElement.GetProperty("status").GetString());
        Assert.Equal("HYPOTHESIS", audience.RootElement.GetProperty("definitions")[0]
            .GetProperty("classification").GetString());
        Assert.False(string.IsNullOrWhiteSpace(audience.RootElement
            .GetProperty("targetingRationale").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(audience.RootElement
            .GetProperty("positioningStatement").GetString()));

        using var mix = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/media-mixes:generate"),
            "planning-mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1_000_000, mix.RootElement.GetProperty("allocations")[0]
            .GetProperty("budgetMinor").GetInt64());
        using var invalidChannelMix = await RawCommandAsync(
            client, Path($"media-mix-versions/{mixId}:update"),
            "planning-mix-expand", 1, new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "RADIO",
                        budgetMinor = 1_000_000,
                        role = "This would widen the OOH-only campaign.",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "This must be rejected by the locked campaign mode.",
            });
        await AssertProblemAsync(
            invalidChannelMix, HttpStatusCode.Conflict, "CAMPAIGN_MODE_LOCKED");
        using var unbalancedMix = await RawCommandAsync(
            client, Path($"media-mix-versions/{mixId}:update"),
            "planning-mix-unbalanced", 1, new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "OOH",
                        budgetMinor = 900_000,
                        role = "Primary local visibility",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Invalid allocation total.",
            });
        await AssertProblemAsync(unbalancedMix, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using var editedMix = await CommandAsync(
            client, Path($"media-mix-versions/{mixId}:update"),
            "planning-mix-edit", 1, new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "OOH",
                        budgetMinor = 1_000_000,
                        role = "Primary local visibility",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Set the channel budget and September running period.",
            });
        Assert.Equal("2026-09-01", editedMix.RootElement.GetProperty("allocations")[0]
            .GetProperty("runningPeriods")[0].GetProperty("start").GetString());
        using var approvedMix = await CommandAsync(
            client, Path($"media-mix-versions/{mixId}:approve"),
            "planning-mix-approve", 2, new { reason = "Solo operator confirms internal mix." });
        Assert.Equal(OperatorId, approvedMix.RootElement.GetProperty("approvedBy").GetGuid());

        using var shortlist = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/shortlists:generate"),
            "planning-shortlist", 1, new { });
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        var candidates = shortlist.RootElement.GetProperty("candidates");
        Assert.Equal(6, candidates.GetArrayLength());
        Assert.Single(candidates.EnumerateArray(), item =>
            !item.GetProperty("isEligible").GetBoolean() &&
            item.GetProperty("rejectionReason").GetString() == "INELIGIBLE_GEOGRAPHY");
        var stale = Assert.Single(candidates.EnumerateArray(), item =>
            !item.GetProperty("isEligible").GetBoolean() &&
            item.GetProperty("rejectionReason").GetString() == "STALE_RATE");
        var confirmed = candidates.EnumerateArray().Single(item =>
            item.GetProperty("isEligible").GetBoolean() &&
            item.GetProperty("rateAmountMinor").GetInt64() == 100_000);
        Assert.Equal(3, confirmed.GetProperty("benchmark").GetProperty("cohortSize").GetInt32());
        var selectedProductId = confirmed.GetProperty("inventoryProductId").GetGuid();
        using var marketComparison = await client.GetAsync(
            Path($"inventory-products/{selectedProductId}/benchmark"));
        marketComparison.EnsureSuccessStatusCode();
        using var marketComparisonJson = JsonDocument.Parse(
            await marketComparison.Content.ReadAsStringAsync());
        Assert.Equal(4, marketComparisonJson.RootElement.GetProperty("cohortSize").GetInt32());
        Assert.Equal("RADIUS_3_KM", marketComparisonJson.RootElement
            .GetProperty("geographyBasis").GetString());
        using var rejectedSelection = await RawCommandAsync(
            client, Path($"shortlist-versions/{shortlistId}:select"),
            "planning-select-rejected", 1,
            new { selectedCandidateIds = new[] { stale.GetProperty("id").GetGuid() },
                reason = "This should fail hard eligibility." });
        await AssertProblemAsync(
            rejectedSelection, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        using var selection = await CommandAsync(
            client, Path($"shortlist-versions/{shortlistId}:select"),
            "planning-select", 1,
            new { selectedCandidateIds = new[] { confirmed.GetProperty("id").GetGuid() },
                reason = "Best confirmed local fit." });
        Assert.Equal("APPROVED", selection.RootElement.GetProperty("status").GetString());

        using var plan = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/media-plans:generate"),
            "planning-plan", 1, new { });
        var planId = plan.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("CONFIRMED", plan.RootElement.GetProperty("supplyConfidence").GetString());
        Assert.Empty(plan.RootElement.GetProperty("objections").EnumerateArray());
        Assert.Equal("supplier-confirmation:email-001", plan.RootElement.GetProperty("lines")[0]
            .GetProperty("supplySource").GetString());
        Assert.Equal(100_000, plan.RootElement.GetProperty("subtotalMinor").GetInt64());
        Assert.Equal(5_000, plan.RootElement.GetProperty("feesMinor").GetInt64());
        Assert.Equal(15_750, plan.RootElement.GetProperty("vatMinor").GetInt64());
        Assert.Equal(120_750, plan.RootElement.GetProperty("totalMinor").GetInt64());

        using var approvedPlan = await CommandAsync(
            client, Path($"media-plan-versions/{planId}:approve"),
            "planning-plan-approve", 1, new { reason = "Confirmed OOH plan is reconciled." });
        Assert.Equal("APPROVED", approvedPlan.RootElement.GetProperty("status").GetString());
        Assert.Equal(OperatorId, approvedPlan.RootElement.GetProperty("approvedBy").GetGuid());

        using var workspace = await client.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        workspace.EnsureSuccessStatusCode();
        using var workspaceJson = JsonDocument.Parse(await workspace.Content.ReadAsStringAsync());
        Assert.Equal("APPROVED", workspaceJson.RootElement.GetProperty("mediaPlan")
            .GetProperty("status").GetString());
        using var crossTenant = await other.GetAsync(Path($"brief-versions/{BriefVersionId}/planning"));
        await AssertProblemAsync(crossTenant, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    private static string Path(string suffix) => $"/api/v1/tenants/{TenantId}/{suffix}";
}
