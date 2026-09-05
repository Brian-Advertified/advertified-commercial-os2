using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class SuppliedBriefPersistenceTests
{
    [Theory]
    [InlineData(MasterDataCodes.CampaignModes.OohOnly, "OOH only billboards")]
    [InlineData(MasterDataCodes.CampaignModes.FullCampaign, "radio and digital")]
    public async Task SuppliedSourceReachesApprovedBriefAndLockedCampaignModeThroughApi(string mode, string media)
    {
        var connection = JourneyConnection();
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection).Options);
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        await SeedAsync(db, tenant, actor);
        db.Memberships.Add(new Membership(new(Guid.NewGuid()), new(tenant), new(actor),
            new(MasterDataCodes.Roles.AgencyAdmin), new(MasterDataCodes.LifecycleStatuses.Active),
            null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.UseEnvironment("Test");
            builder.UseSetting("ConnectionStrings:CommercialDatabase", connection);
            builder.UseSetting("Authentication:Mode", "Deterministic");
            builder.UseSetting("Authentication:DevelopmentIdentity:UserId", actor.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:ActorId", actor.ToString());
            builder.UseSetting("Authentication:DevelopmentIdentity:IdentityType", "human");
            builder.UseDeterministicInventoryProtection();
            builder.UseSetting("InventoryProcessing:Paused", "true");
            builder.UseSetting("SuppliedBrief:Mode", "Deterministic");
        });
        using var client = factory.CreateClient();
        var prefix = $"/api/v1/tenants/{tenant}";
        var source = "  Client: Synthetic Brand\r\nObjective: Raise awareness\r\n" +
            "Audience: Adults\r\nGeography: Gauteng\r\nTiming: October 2026\r\n" +
            $"Budget: ZAR 100000\r\nMedia: {media}\r\n";
        var request = new UnderstandSuppliedBriefRequest("Original request", source,
            InterpretationId: Guid.NewGuid());
        var understood = await UnderstandAsync(client, prefix, request);
        Assert.False(understood.RequiresHumanClarification);
        Assert.Equal(understood.Interpretation, (await UnderstandAsync(client, prefix, request)).Interpretation);
        using var denied = await client.PostAsJsonAsync($"/api/v1/tenants/{Guid.NewGuid()}/briefs:understand", request);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var create = new CreateBriefCommand(understood.Title, actor, "supplied:test:original",
            request.SourceTitle, source, ClientName: understood.ClientName,
            InterpretationId: understood.Interpretation!.Id);
        using var changed = await SendAsync(client, prefix + "/briefs", create with { SourceContent = source.Trim() });
        Assert.Equal(HttpStatusCode.BadRequest, changed.StatusCode);
        var brief = await PostAsync(client, prefix + "/briefs", create);
        var briefId = brief.GetProperty("id").GetGuid();
        var version = await ApproveDraftAsync(client, prefix, briefId, understood.Draft, db, tenant, actor);
        await AssertModeAsync(client, prefix, version, mode);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM commercial.brief_sources
            WHERE tenant_id = {tenant} AND interpretation_id = {understood.Interpretation.Id}
                AND content = {source}
            """).SingleAsync());
    }

    private static string JourneyConnection()
    {
        var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "";
        Assert.StartsWith("advertified_brief_test_", database);
        return new NpgsqlConnectionStringBuilder {
            Host = Environment.GetEnvironmentVariable("PGHOST"), Database = database,
            Username = Environment.GetEnvironmentVariable("PGUSER"),
            Password = Environment.GetEnvironmentVariable("PGPASSWORD") }.ConnectionString;
    }

    private static async Task<SuppliedBriefUnderstandingView> UnderstandAsync(HttpClient client,
        string prefix, UnderstandSuppliedBriefRequest request)
    {
        using var response = await client.PostAsJsonAsync(prefix + "/briefs:understand", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SuppliedBriefUnderstandingView>())!;
    }

    private static async Task<JsonElement> ApproveDraftAsync(HttpClient client, string prefix,
        Guid briefId, SuppliedBriefDraftView draft, GovernanceDbContext db, Guid tenant, Guid actor)
    {
        var command = new CreateBriefVersionCommand(briefId, null, draft.BusinessProblem, draft.Objective,
            draft.Audiences, draft.Geographies, draft.Timing, draft.BudgetMinor, draft.BudgetUnknown,
            draft.Currency, draft.VatStatus, draft.FeesMinor, draft.Constraints, draft.Measurement,
            draft.Facts, draft.Unknowns, draft.Assumptions, draft.Conflicts, []);
        var version = await PostAsync(client, $"{prefix}/briefs/{briefId}/versions", command);
        var path = $"{prefix}/brief-versions/{version.GetProperty("id").GetGuid()}";
        using var denied = await SendAsync(client, path + ":submit", new SubmitBriefVersionCommand(null, null),
            version.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await SeedApprovalPolicyAsync(db, tenant, actor);
        version = await PostAsync(client, path + ":submit", new SubmitBriefVersionCommand(null, null),
            version.GetProperty("version").GetInt64());
        version = await PostAsync(client, path + ":approve", new ApproveBriefVersionCommand("Human reviewed source"),
            version.GetProperty("version").GetInt64());
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Approved, version.GetProperty("status").GetString());
        return version;
    }

    private static Task<int> SeedApprovalPolicyAsync(GovernanceDbContext db, Guid tenant, Guid actor)
    {
        var policy = Guid.NewGuid();
        var version = Guid.NewGuid();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.commercial_policies (id, tenant_id, version, created_at_utc, updated_at_utc)
            VALUES ({policy}, {tenant}, 1, now(), now());
            INSERT INTO commercial.commercial_policy_versions (id, tenant_id, policy_id, version_number,
                markup_basis_points, management_fee_basis_points, commission_basis_points, vat_status_code,
                vat_rate_basis_points, prices_include_vat, currency_code, booking_approval_threshold_minor,
                allow_self_approval, created_by, created_at_utc)
            VALUES ({version}, {tenant}, {policy}, 1, 0, 0, 0, {MasterDataCodes.VatStatuses.Registered},
                1500, false, {MasterDataCodes.Currencies.Zar}, 1000000, true, {actor}, now());
            UPDATE commercial.commercial_policies SET current_version_id = {version}
            WHERE tenant_id = {tenant} AND id = {policy};
            """);
    }

    private static async Task AssertModeAsync(HttpClient client, string prefix, JsonElement version, string selectedMode)
    {
        var path = $"{prefix}/brief-versions/{version.GetProperty("id").GetGuid()}/campaign-mode:select";
        var command = new Advertified.Commercial.Application.Planning.SelectCampaignModeCommand(
            selectedMode, MasterDataCodes.CampaignModeDecisionSources.HumanSelection,
            1m, "Human confirmed supplied scope");
        var mode = await PostAsync(client, path, command);
        Assert.True(mode.GetProperty("isLocked").GetBoolean());
        var other = selectedMode == MasterDataCodes.CampaignModes.OohOnly
            ? MasterDataCodes.CampaignModes.FullCampaign : MasterDataCodes.CampaignModes.OohOnly;
        using var changed = await SendAsync(client, path, command with { Mode = other });
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    private static async Task<JsonElement> PostAsync<T>(HttpClient client, string path, T body, long? version = null)
    {
        using var response = await SendAsync(client, path, body, version);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{path}: {(int)response.StatusCode} {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> SendAsync<T>(HttpClient client, string path, T body, long? version = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return client.SendAsync(request);
    }
}
