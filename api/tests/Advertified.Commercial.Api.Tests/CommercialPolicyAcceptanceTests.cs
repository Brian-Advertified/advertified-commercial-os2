using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class CommercialPolicyAcceptanceTests
{
    private static readonly Guid TenantId =
        Guid.Parse("a8100000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("a8100000-0000-0000-0000-000000000002");
    private static readonly Guid AdminId =
        Guid.Parse("a8200000-0000-0000-0000-000000000001");
    private static readonly Guid CampaignUserId =
        Guid.Parse("a8200000-0000-0000-0000-000000000002");
    private static readonly Guid OtherAdminId =
        Guid.Parse("a8200000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task PolicyIsVersionedIdempotentAndTenantSafe()
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

        await AssertEmptyPolicyAsync(await admin.GetAsync(PolicyPath(TenantId)));

        var first = await SaveAsync(admin, TenantId, "policy-create", 0, Policy(1_500));
        await AssertPolicyAsync(first, 1, 1_500, replayed: false);
        var replay = await SaveAsync(admin, TenantId, "policy-create", 0, Policy(1_500));
        await AssertPolicyAsync(replay, 1, 1_500, replayed: true);

        var second = await SaveAsync(admin, TenantId, "policy-update", 1, Policy(1_750));
        await AssertPolicyAsync(second, 2, 1_750, replayed: false);
        await AssertProblemAsync(
            await SaveAsync(admin, TenantId, "policy-stale", 1, Policy(2_000)),
            HttpStatusCode.Conflict,
            "VERSION_CONFLICT");

        await AssertProblemAsync(
            await SaveAsync(campaignUser, TenantId, "policy-denied", 2, Policy(2_000)),
            HttpStatusCode.Forbidden,
            "TENANT_FORBIDDEN");
        await AssertProblemAsync(
            await otherAdmin.GetAsync(PolicyPath(TenantId)),
            HttpStatusCode.Forbidden,
            "TENANT_FORBIDDEN");
        await AssertEmptyPolicyAsync(await otherAdmin.GetAsync(PolicyPath(OtherTenantId)));
        await AssertMigrationCycleAsync(connectionString);
    }

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        "advertified_commercial_policy",
        "advertified_commercial_policy",
        "advertified-commercial-policy-local-only");

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
            Tenant(TenantId, "commercial-policy-workspace"),
            Tenant(OtherTenantId, "other-commercial-workspace"));
        db.Users.AddRange(
            User(AdminId, "admin@policy.example"),
            User(CampaignUserId, "campaign@policy.example"),
            User(OtherAdminId, "other@policy.example"));
        db.Memberships.AddRange(
            Membership(TenantId, AdminId, "agency_admin", 1),
            Membership(TenantId, CampaignUserId, "agency_campaign_user", 2),
            Membership(OtherTenantId, OtherAdminId, "agency_admin", 3));
        await db.SaveChangesAsync();
    }

    private static async Task AssertMigrationCycleAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        Assert.False(await TableExistsAsync(connectionString, "commercial.commercial_policies"));
        await migrator.MigrateAsync();
        await migrator.MigrateAsync();
        Assert.True(await TableExistsAsync(connectionString, "commercial.commercial_policies"));
    }

    private static async Task<bool> TableExistsAsync(
        string connectionString, string qualifiedName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT to_regclass($1) IS NOT NULL", connection);
        command.Parameters.AddWithValue(qualifiedName);
        return (bool)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Table state was unavailable."));
    }

    private static Tenant Tenant(Guid id, string slug) => new(
        new TenantId(id), new TenantTypeCode("AGENCY"), slug, slug, new Slug(slug),
        new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now);

    private static User User(Guid id, string email) => new(
        new UserId(id), new EmailAddress(email), email, null,
        new LifecycleStatusCode("ACTIVE"), true, Now);

    private static Membership Membership(
        Guid tenantId, Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"a8300000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(tenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, Now);

    private static object Policy(int markupBasisPoints) => new
    {
        markupBasisPoints,
        managementFeeBasisPoints = 500,
        commissionBasisPoints = 1_000,
        vatStatus = "REGISTERED",
        vatRateBasisPoints = 1_500,
        pricesIncludeVat = false,
        currency = "ZAR",
        bookingApprovalThresholdMinor = 5_000_000L,
    };

    private static string PolicyPath(Guid tenantId) =>
        $"/api/v1/tenants/{tenantId}/commercial-policy";

    private static Task<HttpResponseMessage> SaveAsync(
        HttpClient client, Guid tenantId, string key, long version, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, PolicyPath(tenantId))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    private static async Task AssertEmptyPolicyAsync(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("null", (await response.Content.ReadAsStringAsync()).Trim());
        }
    }

    private static async Task AssertPolicyAsync(
        HttpResponseMessage response, int version, int markup, bool replayed)
    {
        using (response)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, content);
            Assert.Equal($"\"{version}\"", response.Headers.ETag?.Tag);
            Assert.Equal(replayed,
                response.Headers.Contains("Idempotency-Replayed"));
            using var json = JsonDocument.Parse(content);
            Assert.Equal(version, json.RootElement.GetProperty("versionNumber").GetInt32());
            Assert.Equal(markup,
                json.RootElement.GetProperty("markupBasisPoints").GetInt32());
        }
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        }
    }
}
