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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
            builder.UseDeterministicTestDependencies();
            builder.UseSetting("InventoryProcessing:Paused", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISuppliedBriefAgentClient>();
                services.AddScoped<ISuppliedBriefAgentClient>(_ =>
                    new SuppliedBriefAgentFixture(input =>
                        SuppliedBriefAgentFixture.Create(input, mode)));
            });
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
        var version = await ApproveDraftAsync(client, prefix, briefId, understood.Draft);
        await AssertModeAsync(client, prefix, version, mode);
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT count(*)::integer AS "Value" FROM commercial.brief_sources
            WHERE tenant_id = {tenant} AND interpretation_id = {understood.Interpretation.Id}
                AND content = {source}
            """).SingleAsync());
    }

    [Fact]
    public async Task BriefClarificationCanImmediatelyFollowInitialUnderstandingThroughRateLimitPolicy()
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
            builder.UseDeterministicTestDependencies();
            builder.UseSetting("InventoryProcessing:Paused", "true");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISuppliedBriefAgentClient>();
                services.AddScoped<ISuppliedBriefAgentClient>(_ => new SuppliedBriefAgentFixture(input =>
                {
                    var mode = input.Clarifications.FirstOrDefault(item =>
                        item.FieldPath == SuppliedBriefFieldPaths.CampaignMode)?.Value;
                    if (mode is MasterDataCodes.CampaignModes.OohOnly or MasterDataCodes.CampaignModes.FullCampaign)
                        return SuppliedBriefAgentFixture.Create(input, mode);
                    var result = SuppliedBriefAgentFixture.Create(input, MasterDataCodes.CampaignModes.FullCampaign);
                    return result with {
                        CampaignMode = null,
                        CampaignModeConfidence = 0m,
                        RequiresHumanClarification = true,
                        CampaignModeRationale = "Campaign scope requires human clarification.",
                        Questions = [new SuppliedBriefQuestionView(
                            SuppliedBriefFieldPaths.CampaignMode,
                            "Should this use only out-of-home media or a full campaign?",
                            true,
                            [MasterDataCodes.CampaignModes.OohOnly, MasterDataCodes.CampaignModes.FullCampaign])],
                    };
                }));
            });
        });
        using var client = factory.CreateClient();
        var prefix = $"/api/v1/tenants/{tenant}";
        var first = await UnderstandAsync(client, prefix, new UnderstandSuppliedBriefRequest(
            "Clarification flow", "Objective: Raise awareness", InterpretationId: Guid.NewGuid()));
        Assert.True(first.RequiresHumanClarification);
        Assert.NotNull(first.Interpretation);
        var second = await UnderstandAsync(client, prefix, new UnderstandSuppliedBriefRequest(
            "Clarification flow",
            "Objective: Raise awareness",
            [new BriefClarificationInput(SuppliedBriefFieldPaths.CampaignMode, MasterDataCodes.CampaignModes.OohOnly)],
            InterpretationId: Guid.NewGuid(),
            ParentInterpretationId: first.Interpretation!.Id));
        Assert.False(second.RequiresHumanClarification);
        Assert.Equal(MasterDataCodes.CampaignModes.OohOnly, second.CampaignMode);
        Assert.DoesNotContain(second.Questions, item => item.FieldPath == SuppliedBriefFieldPaths.CampaignMode);
    }

    private static async Task<SuppliedBriefUnderstandingView> UnderstandAsync(HttpClient client,
        string prefix, UnderstandSuppliedBriefRequest request)
    {
        using var response = await client.PostAsJsonAsync(prefix + "/briefs:understand", request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SuppliedBriefUnderstandingView>())!;
    }

    private static async Task<JsonElement> ApproveDraftAsync(HttpClient client, string prefix,
        Guid briefId, SuppliedBriefDraftView draft)
    {
        var command = new CreateBriefVersionCommand(briefId, null, draft.BusinessProblem, draft.Objective,
            draft.Audiences, draft.Geographies, draft.Timing, draft.BudgetMinor, draft.BudgetUnknown,
            draft.Currency, draft.VatStatus, draft.FeesMinor, draft.MediaRequirements, draft.Constraints, draft.Measurement,
            draft.Facts, draft.Unknowns, draft.Assumptions, draft.Conflicts, []);
        var version = await PostAsync(client, $"{prefix}/briefs/{briefId}/versions", command);
        var path = $"{prefix}/brief-versions/{version.GetProperty("id").GetGuid()}";
        using var denied = await SendAsync(client, path + ":submit", new SubmitBriefVersionCommand(null, null),
            version.GetProperty("version").GetInt64());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await SaveApprovalPolicyAsync(client, prefix);
        version = await PostAsync(client, path + ":submit", new SubmitBriefVersionCommand(null, null),
            version.GetProperty("version").GetInt64());
        version = await PostAsync(client, path + ":approve", new ApproveBriefVersionCommand("Human reviewed source"),
            version.GetProperty("version").GetInt64());
        Assert.Equal(MasterDataCodes.LifecycleStatuses.Approved, version.GetProperty("status").GetString());
        return version;
    }

    private static async Task SaveApprovalPolicyAsync(HttpClient client, string prefix)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, prefix + "/commercial-policy")
        {
            Content = JsonContent.Create(new Advertified.Commercial.Application.CommercialSettings.SaveCommercialPolicyCommand(
                0, 0, 0, MasterDataCodes.VatStatuses.Registered, 1500, false,
                MasterDataCodes.Currencies.Zar, 1000000, true)),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", "\"0\"");
        using var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
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
