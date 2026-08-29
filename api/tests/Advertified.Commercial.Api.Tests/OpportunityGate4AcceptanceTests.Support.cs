using System.Net;
using System.Net.Http.Json;
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

public sealed partial class OpportunityGate4AcceptanceTests
{
    private const string PostgreSqlImage = "pgvector/pgvector:0.8.6-pg16-bookworm";
    private static readonly Guid TenantId =
        Guid.Parse("f1000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("f1000000-0000-0000-0000-000000000002");
    private static readonly Guid OwnerId =
        Guid.Parse("f2000000-0000-0000-0000-000000000001");
    private static readonly Guid ReviewerId =
        Guid.Parse("f2000000-0000-0000-0000-000000000002");
    private static readonly Guid ApproverId =
        Guid.Parse("f2000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 16, 0, 0, TimeSpan.Zero);
    private static readonly string[] EvidenceGaps = ["Conversion baseline not supplied."];

    private static PostgreSqlContainer CreatePostgres() =>
        new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("advertified_gate4")
            .WithUsername("advertified_gate4")
            .WithPassword("advertified-gate4-local-only")
            .Build();

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        Guid userId,
        bool enableRuntime = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", userId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", userId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
            builder.UseSetting(
                "AgentRuntime:Mode",
                enableRuntime ? "InProcessDeterministic" : "Disabled");
            builder.UseSetting("AgentRuntime:PollMilliseconds", "25");
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
            builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        });

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        db.Tenants.AddRange(CreateTenant(TenantId, "gate-four"),
            CreateTenant(OtherTenantId, "gate-four-other"));
        db.Users.AddRange(
            CreateUser(OwnerId, "owner@gate4.example", "Opportunity Owner"),
            CreateUser(ReviewerId, "reviewer@gate4.example", "Evidence Reviewer"),
            CreateUser(ApproverId, "approver@gate4.example", "Strategy Approver"));
        db.Memberships.AddRange(
            CreateMembership(OwnerId, "platform_admin", 1),
            CreateMembership(ReviewerId, "inventory_ops", 2),
            CreateMembership(ApproverId, "advertiser_approver", 3));
        await db.SaveChangesAsync();
    }

    private static Tenant CreateTenant(Guid id, string slug) => new(
        new TenantId(id),
        new TenantTypeCode("AGENCY"),
        $"{slug} legal",
        slug,
        new Slug(slug),
        new LifecycleStatusCode("ACTIVE"),
        "Africa/Johannesburg",
        new CurrencyCode("ZAR"),
        new VatStatusCode("REGISTERED"),
        null,
        "{}",
        Now);

    private static User CreateUser(Guid id, string email, string name) => new(
        new UserId(id),
        new EmailAddress(email),
        name,
        null,
        new LifecycleStatusCode("ACTIVE"),
        true,
        Now);

    private static Membership CreateMembership(Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"f3000000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(TenantId),
        new UserId(userId),
        new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"),
        null,
        Now);

    private static async Task<JsonElement> GetOpportunityAsync(
        HttpClient client,
        Guid opportunityId)
    {
        using var response = await client.GetAsync(
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}");
        using var json = await ReadJsonAsync(response);
        return json.RootElement.Clone();
    }

    private static async Task<JsonElement> WaitForAsync(
        HttpClient client,
        Guid opportunityId,
        Func<JsonElement, bool> complete)
    {
        JsonElement last = default;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            last = await GetOpportunityAsync(client, opportunityId);
            if (complete(last))
            {
                return last;
            }
            await Task.Delay(25);
        }
        throw new TimeoutException(
            $"The deterministic Gate 4 run did not reach its checkpoint: {last.GetRawText()}");
    }

    private static async Task<HttpResponseMessage> SendCommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        T body,
        long? version = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        if (version.HasValue)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version.Value}\"");
        }
        return await client.SendAsync(request);
    }

    private static async Task SendSuccessfulCommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        T body,
        long? version = null)
    {
        using var response = await SendCommandAsync(client, path, key, body, version);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private static async Task AssertDurableLineageAsync(
        string connectionString,
        Guid opportunityId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(3, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.agent_runs WHERE opportunity_id = $1",
            opportunityId));
        Assert.Equal(4, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.agent_run_steps step " +
            "JOIN commercial.agent_runs run ON run.id = step.run_id " +
            "WHERE run.opportunity_id = $1 AND step.status_code = 'COMPLETED'",
            opportunityId));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COALESCE(sum(usage.incremental_cost_minor), 0)::integer " +
            "FROM commercial.ai_usage_ledger usage JOIN commercial.agent_runs run " +
            "ON run.id = usage.run_id WHERE run.opportunity_id = $1",
            opportunityId));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.human_tasks " +
            "WHERE opportunity_id = $1 AND status_code = 'PENDING'",
            opportunityId));
    }

    private static async Task<int> ScalarAsync(
        NpgsqlConnection connection,
        string sql,
        Guid opportunityId)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(opportunityId);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The lineage query returned no result."));
    }
}
