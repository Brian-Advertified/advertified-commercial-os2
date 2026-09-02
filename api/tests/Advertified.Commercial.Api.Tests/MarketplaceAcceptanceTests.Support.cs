using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static readonly Guid SupplierTenantId =
        Guid.Parse("91000000-0000-0000-0000-000000000001");
    private static readonly Guid BuyerTenantId =
        Guid.Parse("91000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherTenantId =
        Guid.Parse("91000000-0000-0000-0000-000000000003");
    private static readonly Guid SupplierUserId =
        Guid.Parse("92000000-0000-0000-0000-000000000001");
    private static readonly Guid BuyerUserId =
        Guid.Parse("92000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherUserId =
        Guid.Parse("92000000-0000-0000-0000-000000000003");
    private static readonly Guid ClientUserId =
        Guid.Parse("92000000-0000-0000-0000-000000000004");
    private static readonly Guid ReviewerUserId =
        Guid.Parse("92000000-0000-0000-0000-000000000005");
    private static readonly Guid ProductId =
        Guid.Parse("93000000-0000-0000-0000-000000000001");
    private static readonly Guid BuyerClientId =
        Guid.Parse("93000000-0000-0000-0000-000000000002");
    private static readonly Guid BuyerBriefId =
        Guid.Parse("93000000-0000-0000-0000-000000000003");
    private static readonly Guid BuyerBriefVersionId =
        Guid.Parse("93000000-0000-0000-0000-000000000004");
    private static readonly Guid BuyerBriefSourceId =
        Guid.Parse("93000000-0000-0000-0000-000000000005");
    private static readonly DateTimeOffset InitialTime =
        new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        "advertified_marketplace", "advertified_marketplace",
        "advertified-marketplace-local-only");

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        Guid userId,
        AdjustableMarketplaceClock clock) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", userId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", userId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
            builder.UseDeterministicInventoryProtection();
            builder.UseSetting("Logging:LogLevel:Default", "Warning");
            builder.ConfigureLogging(logging => logging.AddConsole());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
            });
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
            CreateTenant(SupplierTenantId, "SUPPLIER", "marketplace-supplier"),
            CreateTenant(BuyerTenantId, "AGENCY", "marketplace-buyer"),
            CreateTenant(OtherTenantId, "SUPPLIER", "marketplace-other"));
        db.Users.AddRange(
            CreateUser(SupplierUserId, "supplier@marketplace.example", "Supplier User"),
            CreateUser(BuyerUserId, "buyer@marketplace.example", "Buyer User"),
            CreateUser(OtherUserId, "other@marketplace.example", "Other User"),
            CreateUser(ClientUserId, "client@marketplace.example", "Client Approver"),
            CreateUser(ReviewerUserId, "reviewer@marketplace.example", "Finance Reviewer"));
        db.Memberships.AddRange(
            CreateMembership(SupplierTenantId, SupplierUserId, "supplier_admin", 1),
            CreateMembership(BuyerTenantId, BuyerUserId, "agency_admin", 2),
            CreateMembership(OtherTenantId, OtherUserId, "supplier_admin", 3),
            CreateMembership(BuyerTenantId, ClientUserId, "advertiser_approver", 4),
            CreateMembership(BuyerTenantId, ReviewerUserId, "platform_admin", 5));
        db.ClientAccounts.Add(new ClientAccount(
            new ClientAccountId(BuyerClientId), new TenantId(BuyerTenantId),
            "marketplace-planning-client", "Marketplace Planning Client",
            "Marketplace Planning Client", null, null, "{}",
            new LifecycleStatusCode("ACTIVE"), InitialTime));
        await db.SaveChangesAsync();
        await SeedInventoryAsync(connectionString);
        await SeedBuyerPlanningAsync(connectionString);
    }

    private static Tenant CreateTenant(Guid id, string type, string slug) => new(
        new TenantId(id), new TenantTypeCode(type), $"{slug} legal", slug,
        new Slug(slug), new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", InitialTime);

    private static User CreateUser(Guid id, string email, string name) => new(
        new UserId(id), new EmailAddress(email), name, null,
        new LifecycleStatusCode("ACTIVE"), true, InitialTime);

    private static Membership CreateMembership(
        Guid tenantId, Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"94000000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(tenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, InitialTime);

    private static async Task<HttpResponseMessage> RawCommandAsync<T>(
        HttpClient client, Guid tenantId, string path, string key, long? version, T body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/tenants/{tenantId}/{path}")
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

    private static async Task<JsonDocument> CommandAsync<T>(
        HttpClient client, Guid tenantId, string path, string key, long? version, T body)
    {
        using var response = await RawCommandAsync(client, tenantId, path, key, version, body);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Marketplace command {path} returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task<JsonDocument> ReadAsync(
        HttpClient client, Guid tenantId, string path)
    {
        using var response = await client.GetAsync($"/api/v1/tenants/{tenantId}/{path}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Marketplace query {path} returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
    }

    private sealed class AdjustableMarketplaceClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        internal void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);

        internal void Set(DateTimeOffset value) => utcNow = value;
    }
}
