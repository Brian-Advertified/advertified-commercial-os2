using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static readonly Guid FullBriefId =
        Guid.Parse("74000000-0000-0000-0000-000000000005");
    private static readonly Guid FullBriefSourceId =
        Guid.Parse("74000000-0000-0000-0000-000000000006");
    private static readonly Guid FullBriefVersionId =
        Guid.Parse("74000000-0000-0000-0000-000000000007");

    [Fact]
    [Trait("Category", "Migration")]
    public async Task FullCampaignAcceptsAnActiveNonOohChannel()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await SeedFullCampaignBriefAsync(connectionString);
        await using var factory = CreateFactory(
            connectionString,
            OperatorId,
            configureServices: ConfigureDeterministicPlanningClock);
        using var client = factory.CreateClient();

        using var selectedMode = await CommandAsync(
            client,
            Path($"brief-versions/{FullBriefVersionId}/campaign-mode:select"),
            "full-mode-select",
            1,
            Mode("FULL_CAMPAIGN"));
        Assert.Equal("FULL_CAMPAIGN", selectedMode.RootElement.GetProperty("mode").GetString());
        Assert.Contains("RADIO", selectedMode.RootElement.GetProperty("allowedChannels")
            .EnumerateArray().Select(item => item.GetString()));

        using var audience = await CommandAsync(
            client,
            Path($"brief-versions/{FullBriefVersionId}/audiences:generate"),
            "full-audience",
            1,
            new { });
        using var mix = await CommandAsync(
            client,
            Path($"brief-versions/{FullBriefVersionId}/media-mixes:generate"),
            "full-mix",
            1,
            new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var updated = await CommandAsync(
            client,
            Path($"media-mix-versions/{mixId}:update"),
            "full-radio-allocation",
            1,
            new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "RADIO",
                        budgetMinor = 1_000_000,
                        role = "Extend campaign reach through a governed full-campaign channel.",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Use an active non-OOH channel permitted by FULL_CAMPAIGN.",
            });

        Assert.Equal("RADIO", updated.RootElement.GetProperty("allocations")[0]
            .GetProperty("channel").GetString());
    }

    private static async Task SeedFullCampaignBriefAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var batch = new NpgsqlBatch(connection);
        AddCommand(batch,
            """
            INSERT INTO commercial.campaign_briefs (
                id, tenant_id, client_account_id, title, owner_user_id, status_code,
                version, created_at_utc, updated_at_utc)
            SELECT $1, tenant_id, client_account_id, 'Integrated launch', owner_user_id,
                status_code, 1, $4, $4
            FROM commercial.campaign_briefs WHERE id = $2 AND tenant_id = $3
            """, FullBriefId, BriefId, TenantId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_sources (
                id, tenant_id, brief_id, source_type_code, locator, title, content,
                content_hash, created_by, created_at_utc)
            SELECT $1, tenant_id, $2, source_type_code, 'owner:full-supplied', title,
                content, repeat('f', 64), created_by, $5
            FROM commercial.brief_sources WHERE id = $3 AND tenant_id = $4
            """, FullBriefSourceId, FullBriefId, BriefSourceId, TenantId, Now);
        AddCommand(batch,
            """
            INSERT INTO commercial.brief_versions (
                id, tenant_id, brief_id, source_id, version_no, business_problem,
                objective, audiences_json, geographies_json, timing, budget_minor,
                budget_unknown, currency_code, vat_status_code, fees_minor,
                constraints_json, measurement_json, facts_json, unknowns_json,
                assumptions_json, conflicts_json, evidence_bindings_json, status_code,
                created_by, approved_by, approved_at_utc, version, created_at_utc)
            SELECT $1, tenant_id, $2, $3, 1, business_problem, objective, audiences_json,
                geographies_json, timing, budget_minor, budget_unknown, currency_code,
                vat_status_code, fees_minor, constraints_json, measurement_json,
                facts_json, unknowns_json, assumptions_json, conflicts_json,
                evidence_bindings_json, status_code, created_by, approved_by,
                approved_at_utc, 1, $6
            FROM commercial.brief_versions WHERE id = $4 AND tenant_id = $5
            """, FullBriefVersionId, FullBriefId, FullBriefSourceId,
            BriefVersionId, TenantId, Now);
        AddCommand(batch,
            "UPDATE commercial.campaign_briefs SET current_draft_version_id = $1, " +
            "approved_version_id = $1 WHERE id = $2 AND tenant_id = $3",
            FullBriefVersionId, FullBriefId, TenantId);
        await batch.ExecuteNonQueryAsync();
    }
}
