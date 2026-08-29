using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CommercialFoundationApiAcceptanceTests
{
    private const string PostgreSqlImage = "pgvector/pgvector:0.8.6-pg16-bookworm";
    private static readonly Guid TenantId =
        Guid.Parse("e1000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId =
        Guid.Parse("e1000000-0000-0000-0000-000000000002");
    private static readonly Guid UserId =
        Guid.Parse("e2000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Migration")]
    public async Task FoundationRoutesEnforceCommandAndTenantContracts()
    {
        await using var postgres = new PostgreSqlBuilder(PostgreSqlImage)
            .WithDatabase("advertified_gate2_api")
            .WithUsername("advertified_gate2")
            .WithPassword("advertified-gate2-local-only")
            .Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);

        await using var factory = CreateFactory(connectionString);
        using var client = factory.CreateClient();

        await AssertIdentityAndTenantReadsAsync(client);
        await AssertTenantCommandSemanticsAsync(client);
        await AssertBusinessRoutesAsync(client);

        using var denied = await client.GetAsync($"/api/v1/tenants/{OtherTenantId}");
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        await AssertConsequencesAsync(connectionString);
        await AssertBrowserSessionJourneyAsync(connectionString);
    }

    private static async Task AssertIdentityAndTenantReadsAsync(HttpClient client)
    {
        using var tenant = await client.GetAsync($"/api/v1/tenants/{TenantId}");
        Assert.Equal(HttpStatusCode.OK, tenant.StatusCode);
        Assert.Equal("\"1\"", tenant.Headers.ETag?.Tag);

        using var workspaces = await client.GetAsync("/api/v1/workspaces");
        using var workspaceJson = await ReadJsonAsync(workspaces);
        Assert.Single(workspaceJson.RootElement.EnumerateArray());
        Assert.Equal(1, workspaceJson.RootElement[0].GetProperty("version").GetInt64());

        using var memberships = await client.GetAsync(
            $"/api/v1/tenants/{TenantId}/memberships");
        using var membershipsJson = await ReadJsonAsync(memberships);
        Assert.Single(membershipsJson.RootElement.GetProperty("items").EnumerateArray());
    }

    private static async Task AssertTenantCommandSemanticsAsync(HttpClient client)
    {
        var tenantUpdate = new
        {
            legalName = "Updated Tenant Legal",
            tradingName = "Updated Tenant",
            settingsJson = "{\"planningMode\":\"guided\"}",
        };
        using var missingVersion = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}",
            "update-tenant-no-version",
            tenantUpdate);
        await AssertProblemAsync(
            missingVersion,
            HttpStatusCode.PreconditionRequired,
            "PRECONDITION_REQUIRED");

        using var updated = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}",
            "update-tenant-1",
            tenantUpdate,
            1);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("\"2\"", updated.Headers.ETag?.Tag);
        Assert.True(Guid.TryParse(
            updated.Headers.GetValues("X-Correlation-ID").Single(),
            out _));

        using var replayed = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}",
            "update-tenant-1",
            tenantUpdate,
            1);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.Equal("true", replayed.Headers.GetValues("Idempotency-Replayed").Single());

        using var changedPayload = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}",
            "update-tenant-1",
            tenantUpdate with { tradingName = "Different Tenant" },
            1);
        await AssertProblemAsync(changedPayload, HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT");

        using var stale = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}",
            "update-tenant-stale",
            tenantUpdate,
            1);
        await AssertProblemAsync(stale, HttpStatusCode.Conflict, "VERSION_CONFLICT");
    }

    private static async Task AssertBusinessRoutesAsync(HttpClient client)
    {
        using var missingKey = await client.PostAsJsonAsync(
            $"/api/v1/tenants/{TenantId}/agencies",
            new
            {
                externalReference = "missing-key",
                legalName = "Missing Key Legal",
                tradingName = "Missing Key",
                website = (string?)null,
            });
        await AssertProblemAsync(
            missingKey,
            HttpStatusCode.BadRequest,
            "IDEMPOTENCY_KEY_REQUIRED");

        var clientView = await CreateClientAccountAsync(client);
        var clientAccountId = clientView.GetProperty("id").GetGuid();
        var firstAgencyId = await CreateAgencyAsync(client, "agency-1", "Alpha Agency");
        var secondAgencyId = await CreateAgencyAsync(client, "agency-2", "Beta Agency");
        await CreateContactAsync(client, clientAccountId);
        await UpdateUserAsync(client);

        using var firstPage = await client.GetAsync(
            $"/api/v1/tenants/{TenantId}/agencies?limit=1");
        using var firstPageJson = await ReadJsonAsync(firstPage);
        var cursor = firstPageJson.RootElement.GetProperty("nextCursor").GetString();
        Assert.Equal(firstAgencyId, firstPageJson.RootElement.GetProperty("items")[0]
            .GetProperty("id").GetGuid());
        using var secondPage = await client.GetAsync(
            $"/api/v1/tenants/{TenantId}/agencies?limit=1&cursor={Uri.EscapeDataString(cursor!)}");
        using var secondPageJson = await ReadJsonAsync(secondPage);
        Assert.Equal(secondAgencyId, secondPageJson.RootElement.GetProperty("items")[0]
            .GetProperty("id").GetGuid());

        using var contacts = await client.GetAsync($"/api/v1/tenants/{TenantId}/contacts");
        using var contactsJson = await ReadJsonAsync(contacts);
        Assert.Single(contactsJson.RootElement.GetProperty("items").EnumerateArray());
    }

    private static async Task<JsonElement> CreateClientAccountAsync(HttpClient client)
    {
        using var response = await SendCommandAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/tenants/{TenantId}/client-accounts",
            "create-client-1",
            new
            {
                externalReference = "client-1",
                legalName = "Client One Legal",
                tradingName = "Client One",
                website = "https://client.example",
                industry = "Retail",
                billingProfileJson = "{}",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.Clone();
    }

    private static async Task<Guid> CreateAgencyAsync(
        HttpClient client,
        string key,
        string tradingName)
    {
        using var response = await SendCommandAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/tenants/{TenantId}/agencies",
            key,
            new
            {
                externalReference = key,
                legalName = $"{tradingName} Legal",
                tradingName,
                website = (string?)null,
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CreateContactAsync(HttpClient client, Guid clientAccountId)
    {
        using var response = await SendCommandAsync(
            client,
            HttpMethod.Post,
            $"/api/v1/tenants/{TenantId}/contacts",
            "create-contact-1",
            new
            {
                clientAccountId,
                name = "Casey Client",
                jobTitle = "Marketing Director",
                email = "casey@example.com",
                phone = "+27110000000",
                purposeCode = "CAMPAIGN",
                consentBasis = "Supplied for campaign coordination.",
                retainUntil = new DateOnly(2027, 8, 29),
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task UpdateUserAsync(HttpClient client)
    {
        using var response = await SendCommandAsync(
            client,
            HttpMethod.Put,
            $"/api/v1/tenants/{TenantId}/me",
            "update-user-1",
            new { displayName = "Gate Two User", phone = "+27112223333" },
            1);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"2\"", response.Headers.ETag?.Tag);
    }

    private static async Task<HttpResponseMessage> SendCommandAsync<T>(
        HttpClient client,
        HttpMethod method,
        string path,
        string idempotencyKey,
        T body,
        long? expectedVersion = null)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
        if (expectedVersion.HasValue)
        {
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion.Value}\"");
        }

        return await client.SendAsync(request);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connectionString);
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", UserId.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
        });
    }

    private static async Task SeedAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        await db.Database.MigrateAsync();
        await new MasterDataBootstrapper(db, TimeProvider.System).ApplyAsync();
        db.Tenants.AddRange(CreateTenant(TenantId, "gate-two"), CreateTenant(OtherTenantId, "other"));
        db.Users.Add(new User(
            new UserId(UserId),
            new EmailAddress("gate.two@example.com"),
            "Gate Two User",
            null,
            new LifecycleStatusCode("ACTIVE"),
            true,
            Now));
        db.Memberships.Add(new Membership(
            new MembershipId(Guid.Parse("e3000000-0000-0000-0000-000000000001")),
            new TenantId(TenantId),
            new UserId(UserId),
            new RoleCode("agency_admin"),
            new LifecycleStatusCode("ACTIVE"),
            null,
            Now));
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

    private static async Task AssertConsequencesAsync(string connectionString)
    {
        await using var db = new GovernanceDbContext(
            new DbContextOptionsBuilder<GovernanceDbContext>()
                .UseNpgsql(connectionString).Options);
        Assert.Equal(6, await db.IdempotencyRecords.CountAsync());
        Assert.Equal(6, await db.OutboxMessages.CountAsync());
        Assert.Equal(7, await db.AuditEvents.CountAsync());
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
        Assert.True(Guid.TryParse(json.RootElement.GetProperty("correlationId").GetString(), out _));
    }
}
