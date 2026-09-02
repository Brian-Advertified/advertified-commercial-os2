using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class InventoryAcceptanceTests
{
    private static readonly Guid TenantId =
        Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("61000000-0000-0000-0000-000000000002");
    private static readonly Guid ImporterId =
        Guid.Parse("62000000-0000-0000-0000-000000000001");
    private static readonly Guid ReviewerId =
        Guid.Parse("62000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherUserId =
        Guid.Parse("62000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 18, 0, 0, TimeSpan.Zero);

    private static PostgreSqlContainer CreatePostgres() => DisposablePostgres.Create(
        "advertified_inventory", "advertified_inventory", "advertified-inventory-local-only");

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
        builder.UseSetting("InventoryProtection:ObjectStoreMode", "InMemory");
        builder.UseSetting("InventoryProtection:ScannerMode", "Deterministic");
        builder.UseSetting("InventoryExtraction:Mode", "Deterministic");
        builder.UseSetting("InventoryEmbedding:Mode", "Deterministic");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.ConfigureLogging(logging => logging.AddConsole());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IInventoryDocumentExtractionAdapter>();
            services.AddScoped<IInventoryDocumentExtractionAdapter,
                InventoryWorkflowFixtureExtractionAdapter>();
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
        db.Tenants.AddRange(CreateTenant(TenantId, "inventory-one"),
            CreateTenant(OtherTenantId, "inventory-two"));
        db.Users.AddRange(
            CreateUser(ImporterId, "importer@inventory.example", "Inventory Importer"),
            CreateUser(ReviewerId, "reviewer@inventory.example", "Inventory Reviewer"),
            CreateUser(OtherUserId, "other@inventory.example", "Other Tenant User"));
        db.Memberships.AddRange(
            CreateMembership(TenantId, ImporterId, "platform_admin", 1),
            CreateMembership(TenantId, ReviewerId, "inventory_ops", 2),
            CreateMembership(OtherTenantId, OtherUserId, "platform_admin", 3));
        await db.SaveChangesAsync();
    }

    private static Tenant CreateTenant(Guid id, string slug) => new(
        new TenantId(id), new TenantTypeCode("SUPPLIER"), $"{slug} legal", slug,
        new Slug(slug), new LifecycleStatusCode("ACTIVE"), "Africa/Johannesburg",
        new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"), null, "{}", Now);

    private static User CreateUser(Guid id, string email, string name) => new(
        new UserId(id), new EmailAddress(email), name, null,
        new LifecycleStatusCode("ACTIVE"), true, Now);

    private static Membership CreateMembership(
        Guid tenantId, Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"63000000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(tenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, Now);

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string key,
        string supplier,
        FileFixture fixture)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new StringContent(supplier), "supplierName");
        var file = new ByteArrayContent(fixture.Content);
        file.Headers.ContentType = new(fixture.MediaType);
        body.Add(file, "source", fixture.FileName);
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v1/tenants/{TenantId}/inventory-imports")
        {
            Content = body,
        };
        AddCommandHeaders(request, key, null);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> CommandAsync<T>(
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
        AddCommandHeaders(request, key, version);
        return await client.SendAsync(request);
    }

    private static void AddCommandHeaders(
        HttpRequestMessage request,
        string key,
        long? version)
    {
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        if (version.HasValue)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version.Value}\"");
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
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
