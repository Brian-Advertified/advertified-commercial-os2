using System.Net;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static readonly Guid SuccessorBriefVersionId =
        Guid.Parse("74000000-0000-0000-0000-000000000004");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task CampaignModeIsImmutableAcrossEveryVersionOfTheBrief()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await SeedApprovedSuccessorAsync(connectionString);
        await using var factory = CreateFactory(connectionString, OperatorId);
        using var client = factory.CreateClient();

        using var firstMode = await CommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/campaign-mode:select"),
            "aggregate-mode-first",
            1,
            Mode("OOH_ONLY"));
        Assert.Equal("OOH_ONLY", firstMode.RootElement.GetProperty("mode").GetString());

        using var changed = await RawCommandAsync(
            client,
            Path($"brief-versions/{SuccessorBriefVersionId}/campaign-mode:select"),
            "aggregate-mode-change",
            1,
            Mode("FULL_CAMPAIGN"));
        await AssertProblemAsync(
            changed, HttpStatusCode.Conflict, "CAMPAIGN_MODE_LOCKED");

        using var retained = await CommandAsync(
            client,
            Path($"brief-versions/{SuccessorBriefVersionId}/campaign-mode:select"),
            "aggregate-mode-retained",
            1,
            Mode("OOH_ONLY"));
        Assert.Equal("OOH_ONLY", retained.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    [Trait("Category", "Migration")]
    public async Task MediaPlanRejectsCombinedChannelCostAboveItsAllocation()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString, briefBudgetMinor: 200_000);
        await using var factory = CreateFactory(
            connectionString, OperatorId, configureServices: ConfigureDeterministicPlanningClock);
        using var client = factory.CreateClient();

        using var mode = await CommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/campaign-mode:select"),
            "reconcile-mode",
            1,
            Mode("OOH_ONLY"));
        using var audience = await CommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/audiences:generate"),
            "reconcile-audience",
            1,
            new { });
        using var mix = await CommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/media-mixes:generate"),
            "reconcile-mix",
            1,
            new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var scheduledMix = await CommandAsync(
            client,
            Path($"media-mix-versions/{mixId}:update"),
            "reconcile-mix-period",
            1,
            new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "OOH",
                        budgetMinor = 200_000,
                        role = "Primary local visibility",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Reconcile the constrained OOH allocation.",
            });
        using var approvedMix = await CommandAsync(
            client,
            Path($"media-mix-versions/{mixId}:approve"),
            "reconcile-mix-approve",
            2,
            new { reason = "Confirm the constrained mix." });
        using var shortlist = await CommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/shortlists:generate"),
            "reconcile-shortlist",
            1,
            new { });
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();
        var selectedIds = shortlist.RootElement.GetProperty("candidates")
            .EnumerateArray()
            .Where(item => item.GetProperty("isEligible").GetBoolean())
            .Take(2)
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();
        Assert.Equal(2, selectedIds.Length);
        using var selection = await CommandAsync(
            client,
            Path($"shortlist-versions/{shortlistId}:select"),
            "reconcile-select",
            1,
            new
            {
                selectedCandidateIds = selectedIds,
                reason = "Exercise aggregate per-channel reconciliation.",
            });

        using var blocked = await RawCommandAsync(
            client,
            Path($"brief-versions/{BriefVersionId}/media-plans:generate"),
            "reconcile-plan",
            1,
            new { });
        await AssertProblemAsync(
            blocked, HttpStatusCode.Conflict, "PLANNING_APPROVAL_BLOCKED");
    }

    private static object Mode(string mode) => new
    {
        mode,
        decisionSource = "HUMAN_CLARIFICATION",
        confidence = 1m,
        reason = "Retain the immutable campaign scope.",
    };

    private static async Task SeedApprovedSuccessorAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            SELECT @successorId, tenant_id, brief_id, source_id, 2, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, 1, @createdAt
            FROM commercial.brief_versions
            WHERE tenant_id = @tenantId AND id = @sourceVersionId
            """,
            connection);
        command.Parameters.AddWithValue("successorId", SuccessorBriefVersionId);
        command.Parameters.AddWithValue("createdAt", Now.AddMinutes(1));
        command.Parameters.AddWithValue("tenantId", TenantId);
        command.Parameters.AddWithValue("sourceVersionId", BriefVersionId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

}
