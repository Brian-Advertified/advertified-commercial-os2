using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class ProposalAcceptanceTests
{
    private static readonly Guid TenantId = Guid.Parse("81000000-0000-0000-0000-000000000001");
    private static readonly Guid OperatorId = Guid.Parse("82000000-0000-0000-0000-000000000001");
    private static readonly Guid ClientUserId = Guid.Parse("82000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherUserId = Guid.Parse("82000000-0000-0000-0000-000000000003");
    private static readonly Guid ReviewerUserId = Guid.Parse("82000000-0000-0000-0000-000000000004");
    private static readonly Guid ClientId = Guid.Parse("83000000-0000-0000-0000-000000000001");
    private static readonly Guid BriefId = Guid.Parse("84000000-0000-0000-0000-000000000001");
    private static readonly Guid BriefVersionId = Guid.Parse("84000000-0000-0000-0000-000000000002");
    private static readonly Guid BriefSourceId = Guid.Parse("84000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

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
        db.Tenants.Add(new Tenant(
            new TenantId(TenantId), new TenantTypeCode("AGENCY"), "Proposal Agency",
            "Proposal Agency", new Slug("proposal-agency"), new LifecycleStatusCode("ACTIVE"),
            "Africa/Johannesburg", new CurrencyCode("ZAR"), new VatStatusCode("REGISTERED"),
            null, "{}", Now));
        db.Users.AddRange(
            CreateUser(OperatorId, "operator@proposal.example", "Agency Operator"),
            CreateUser(ClientUserId, "client@proposal.example", "Client Approver"),
            CreateUser(OtherUserId, "other@proposal.example", "Other User"),
            CreateUser(ReviewerUserId, "reviewer@proposal.example", "Proposal Reviewer"));
        db.Memberships.AddRange(
            CreateMembership(OperatorId, "agency_admin", 1),
            CreateMembership(ClientUserId, "advertiser_approver", 2),
            CreateMembership(OtherUserId, "advertiser_approver", 3),
            CreateMembership(ReviewerUserId, "agency_campaign_user", 4));
        db.ClientAccounts.Add(new ClientAccount(
            new ClientAccountId(ClientId), new TenantId(TenantId), "proposal-client",
            "Proposal Client", "Proposal Client", null, null, "{}",
            new LifecycleStatusCode("ACTIVE"), Now));
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO commercial.commercial_policies (
                id, tenant_id, current_version_id, version, created_at_utc, updated_at_utc)
            VALUES (
                '88000000-0000-0000-0000-000000000001',
                '81000000-0000-0000-0000-000000000001', NULL, 1,
                clock_timestamp(), clock_timestamp());
            INSERT INTO commercial.commercial_policy_versions (
                id, tenant_id, policy_id, version_number, markup_basis_points,
                management_fee_basis_points, commission_basis_points, vat_status_code,
                vat_rate_basis_points, prices_include_vat, currency_code,
                booking_approval_threshold_minor, allow_self_approval,
                created_by, created_at_utc)
            VALUES (
                '88000000-0000-0000-0000-000000000002',
                '81000000-0000-0000-0000-000000000001',
                '88000000-0000-0000-0000-000000000001', 1,
                0, 0, 0, 'REGISTERED', 1500, false, 'ZAR', 100000000, true,
                '82000000-0000-0000-0000-000000000001', clock_timestamp());
            UPDATE commercial.commercial_policies
            SET current_version_id = '88000000-0000-0000-0000-000000000002'
            WHERE tenant_id = '81000000-0000-0000-0000-000000000001';
            """);
        await SeedProposalPrerequisitesAsync(connectionString);
    }

    private static User CreateUser(Guid id, string email, string name) => new(
        new UserId(id), new EmailAddress(email), name, null,
        new LifecycleStatusCode("ACTIVE"), true, Now);

    private static Membership CreateMembership(Guid userId, string role, int ordinal) => new(
        new MembershipId(Guid.Parse($"85000000-0000-0000-0000-{ordinal:D12}")),
        new TenantId(TenantId), new UserId(userId), new RoleCode(role),
        new LifecycleStatusCode("ACTIVE"), null, Now);

    private static string Path(string suffix) => $"/api/v1/tenants/{TenantId}/{suffix}";

    private static async Task<JsonDocument> CommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        long? version,
        T body)
    {
        using var response = await RawCommandAsync(client, path, key, version, body);
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"Proposal command {path} returned {(int)response.StatusCode}: {content}");
        return JsonDocument.Parse(content);
    }

    private static async Task<HttpResponseMessage> RawCommandAsync<T>(
        HttpClient client,
        string path,
        string key,
        long? version,
        T body)
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
