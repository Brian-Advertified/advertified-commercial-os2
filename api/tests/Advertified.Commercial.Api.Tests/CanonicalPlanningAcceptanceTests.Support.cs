using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static readonly Guid TenantId = Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid OperatorId = Guid.Parse("72000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("72000000-0000-0000-0000-000000000002");
    private static readonly Guid ClientId = Guid.Parse("73000000-0000-0000-0000-000000000001");
    private static readonly Guid BriefId = Guid.Parse("74000000-0000-0000-0000-000000000001");
    private static readonly Guid BriefVersionId = Guid.Parse("74000000-0000-0000-0000-000000000002");
    private static readonly Guid BriefSourceId = Guid.Parse("74000000-0000-0000-0000-000000000003");
    private static readonly Guid SupplierId = Guid.Parse("75000000-0000-0000-0000-000000000001");
    private static readonly Guid ImportId = Guid.Parse("75000000-0000-0000-0000-000000000002");
    private static readonly Guid CandidateId = Guid.Parse("75000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 18, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        "advertified_planning", "advertified_planning", "advertified-planning-local-only");

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
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.ConfigureLogging(logging => logging.AddConsole());
    });

    private static async Task SeedAsync(string connectionString)
    {
        await DisposablePostgres.EnableRequiredExtensionsAsync(connectionString);
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        db.Tenants.AddRange(
            CreateTenant(TenantId, "canonical-planning"),
            CreateTenant(OtherTenantId, "canonical-planning-other"));
        db.Users.AddRange(
            CreateUser(OperatorId, "operator@planning.example", "Solo Agency Operator"),
            CreateUser(OtherUserId, "other@planning.example", "Other Tenant User"));
        db.Memberships.AddRange(
            CreateMembership(TenantId, OperatorId, "agency_admin", 1),
            CreateMembership(OtherTenantId, OtherUserId, "agency_admin", 2));
        db.ClientAccounts.Add(new ClientAccount(
            new ClientAccountId(ClientId), new TenantId(TenantId), "planning-client",
            "Planning Client", "Planning Client", null, null, "{}",
            new LifecycleStatusCode("ACTIVE"), Now));
        await db.SaveChangesAsync();
        await SeedPlanningPrerequisitesAsync(connectionString);
    }

    private static Tenant CreateTenant(Guid id, string slug) => new(
        new TenantId(id), new TenantTypeCode("AGENCY"), $"{slug} legal", slug,
        new Slug(slug), new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now);

    private static User CreateUser(Guid id, string email, string name) => new(
        new UserId(id), new EmailAddress(email), name, null,
        new LifecycleStatusCode("ACTIVE"), true, Now);

    private static Membership CreateMembership(
        Guid tenantId,
        Guid userId,
        string role,
        int ordinal) => new(
        new MembershipId(Guid.Parse($"76000000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(tenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, Now);

    private static async Task<JsonDocument> CommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        long version,
        T body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Planning command {path} returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task<HttpResponseMessage> RawCommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        long version,
        T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        System.Net.HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }
}
