using System.Net;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class AgentOperationsAcceptanceTests
{
    private static readonly Guid TenantId =
        Guid.Parse("aa100000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("aa100000-0000-0000-0000-000000000002");
    private static readonly Guid AdminId =
        Guid.Parse("aa200000-0000-0000-0000-000000000001");
    private static readonly Guid CampaignUserId =
        Guid.Parse("aa200000-0000-0000-0000-000000000002");
    private static readonly Guid OtherAdminId =
        Guid.Parse("aa200000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task AgencyAdminSeesTenantAgentBudgetsCostsAndExceptions()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        await using var adminFactory = CreateFactory(connectionString, AdminId);
        await using var campaignFactory = CreateFactory(connectionString, CampaignUserId);
        await using var otherFactory = CreateFactory(connectionString, OtherAdminId);
        using var admin = adminFactory.CreateClient();
        using var campaignUser = campaignFactory.CreateClient();
        using var otherAdmin = otherFactory.CreateClient();

        using var response = await admin.GetAsync(OperationsPath(TenantId));
        using var json = await ReadJsonAsync(response);
        AssertSummary(json.RootElement);
        AssertAgentBudget(json.RootElement);
        AssertUsageAndRun(json.RootElement);

        await AssertProblemAsync(
            await campaignUser.GetAsync(OperationsPath(TenantId)),
            HttpStatusCode.Forbidden);
        await AssertProblemAsync(
            await otherAdmin.GetAsync(OperationsPath(TenantId)),
            HttpStatusCode.Forbidden);
    }

    private static void AssertSummary(JsonElement root)
    {
        Assert.Equal("USD", root.GetProperty("currency").GetString());
        Assert.Equal("deterministic", root.GetProperty("provider").GetString());
        Assert.False(root.GetProperty("liveProviderEnabled").GetBoolean());
        Assert.Equal(3, root.GetProperty("totalIncrementalCostMinor").GetInt64());
        Assert.Equal(1, root.GetProperty("durableRunCount").GetInt32());
        Assert.Equal(1, root.GetProperty("attentionRunCount").GetInt32());
        Assert.Equal(11, root.GetProperty("agents").GetArrayLength());
    }

    private static void AssertAgentBudget(JsonElement root)
    {
        var agent = root.GetProperty("agents").EnumerateArray().Single(item =>
            item.GetProperty("agentCode").GetString() == "business_interpretation");
        Assert.Equal("Business Interpretation Agent", agent.GetProperty("displayLabel").GetString());
        Assert.Equal("fixture-v1", agent.GetProperty("model").GetString());
        Assert.Equal(0, agent.GetProperty("costCapMinor").GetInt64());
        Assert.Equal(1, agent.GetProperty("usageCount").GetInt32());
        Assert.Equal(3, agent.GetProperty("incrementalCostMinor").GetInt64());
    }

    private static void AssertUsageAndRun(JsonElement root)
    {
        var usage = Assert.Single(root.GetProperty("recentUsage").EnumerateArray());
        Assert.Equal("bedrock", usage.GetProperty("provider").GetString());
        Assert.Equal(10, usage.GetProperty("units").GetInt64());
        Assert.Equal(3, usage.GetProperty("incrementalCostMinor").GetInt64());
        var run = Assert.Single(root.GetProperty("recentRuns").EnumerateArray());
        Assert.Equal("REVIEW_REQUIRED", run.GetProperty("status").GetString());
        Assert.Equal("AGENT_OUTPUT_INVALID", run.GetProperty("errorCode").GetString());
    }

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        "advertified_agent_operations",
        "advertified_agent_operations",
        "advertified-agent-operations-local-only");

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        Guid userId) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
        builder.UseSetting("Authentication:Mode", "Deterministic");
        builder.UseSetting("Authentication:DevelopmentIdentity:UserId", userId.ToString());
        builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", userId.ToString());
        builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
        builder.UseDeterministicInventoryProtection();
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    });

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        db.Tenants.AddRange(
            Tenant(TenantId, "agent-operations"),
            Tenant(OtherTenantId, "other-agent-operations"));
        db.Users.AddRange(
            User(AdminId, "admin@agents.example"),
            User(CampaignUserId, "campaign@agents.example"),
            User(OtherAdminId, "other@agents.example"));
        db.Memberships.AddRange(
            Membership(TenantId, AdminId, "agency_admin", 1),
            Membership(TenantId, CampaignUserId, "agency_campaign_user", 2),
            Membership(OtherTenantId, OtherAdminId, "agency_admin", 3));
        await db.SaveChangesAsync();
        await SeedUsageAsync(connectionString);
    }

    private static async Task SeedUsageAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = SeedSql;
        command.Parameters.AddWithValue("tenant", TenantId);
        command.Parameters.AddWithValue("admin", AdminId);
        command.Parameters.AddWithValue("now", Now);
        await command.ExecuteNonQueryAsync();
    }

    private const string SeedSql = """
        INSERT INTO commercial.client_accounts (
            id, tenant_id, external_reference, legal_name, trading_name,
            billing_profile_json, status_code, version, created_at_utc, updated_at_utc)
        VALUES ('aa300000-0000-0000-0000-000000000001', @tenant, 'agent-client',
            'Agent Client', 'Agent Client', '{}'::jsonb, 'ACTIVE', 1, @now, @now);
        INSERT INTO commercial.opportunities (
            id, tenant_id, client_account_id, title, source_type_code, owner_user_id,
            stage_code, version, created_at_utc, updated_at_utc)
        VALUES ('aa400000-0000-0000-0000-000000000001', @tenant,
            'aa300000-0000-0000-0000-000000000001', 'Agent cost review', 'DISCOVERY',
            @admin, 'STRATEGY_READY', 1, @now, @now);
        INSERT INTO commercial.agent_runs (
            id, tenant_id, opportunity_id, run_kind_code, status_code, input_version,
            requested_by, correlation_id, current_step_code, attempts, error_code,
            version, created_at_utc, updated_at_utc)
        VALUES ('aa500000-0000-0000-0000-000000000001', @tenant,
            'aa400000-0000-0000-0000-000000000001', 'INTERPRETATION',
            'REVIEW_REQUIRED', 1, @admin, 'aa600000-0000-0000-0000-000000000001',
            'INTERPRETATION', 1, 'AGENT_OUTPUT_INVALID', 2, @now, @now);
        INSERT INTO commercial.agent_run_steps (
            id, tenant_id, run_id, step_code, agent_code, status_code, input_hash,
            attempt_count, created_at_utc, updated_at_utc)
        VALUES ('aa700000-0000-0000-0000-000000000001', @tenant,
            'aa500000-0000-0000-0000-000000000001', 'INTERPRETATION',
            'business_interpretation', 'FAILED', repeat('a', 64), 1, @now, @now);
        INSERT INTO commercial.ai_usage_ledger (
            id, tenant_id, run_id, step_id, provider_code, model_code, units,
            tool_calls, incremental_cost_minor, cache_status_code,
            provider_request_id, recorded_at_utc)
        VALUES ('aa800000-0000-0000-0000-000000000001', @tenant,
            'aa500000-0000-0000-0000-000000000001',
            'aa700000-0000-0000-0000-000000000001', 'bedrock',
            'anthropic.claude-test-v1', 10, 0, 3, 'LIVE', 'provider-request-1', @now);
        """;

    private static Tenant Tenant(Guid id, string slug) => new(
        new TenantId(id), new TenantTypeCode("AGENCY"), slug, slug, new Slug(slug),
        new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now);

    private static User User(Guid id, string email) => new(
        new UserId(id), new EmailAddress(email), email, null,
        new LifecycleStatusCode("ACTIVE"), true, Now);

    private static Membership Membership(
        Guid tenantId, Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"aa900000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(tenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, Now);

    private static string OperationsPath(Guid tenantId) =>
        $"/api/v1/tenants/{tenantId}/agent-operations";

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, content);
        return JsonDocument.Parse(content);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("TENANT_FORBIDDEN", json.RootElement.GetProperty("code").GetString());
        }
    }
}
