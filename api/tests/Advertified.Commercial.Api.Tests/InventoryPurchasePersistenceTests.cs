using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Proposal;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed class InventoryPurchasePersistenceTests
{
    private static readonly JsonSerializerOptions WebJson =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ApprovedBuyingQuantityControlsShortlistPlanAndProposalSnapshot()
    {
        var connection = Connection();
        await CanonicalPlanningAcceptanceTests.SeedAsync(connection, 10000000, initializeSchema: false);
        await using var db = new GovernanceDbContext(new DbContextOptionsBuilder<GovernanceDbContext>()
            .UseNpgsql(connection).Options);
        var rate = await InsertRateAsync(db);
        await using var factory = CanonicalPlanningAcceptanceTests.CreateFactory(connection,
            CanonicalPlanningAcceptanceTests.OperatorId,
            configureServices: CanonicalPlanningAcceptanceTests.ConfigureDeterministicPlanningClock)
            .WithWebHostBuilder(builder => {
                builder.UseSetting("AgentRuntime:Mode", "Disabled");
                builder.UseSetting("InventoryProcessing:Paused", "true");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPlanningAgentClient>();
                    services.AddScoped<
                        IPlanningAgentClient,
                        PlanningAgentFixture>();
                });
            });
        using var client = factory.CreateClient();
        var prefix = $"/api/v1/tenants/{CanonicalPlanningAcceptanceTests.TenantId}";
        var brief = $"{prefix}/brief-versions/{CanonicalPlanningAcceptanceTests.BriefVersionId}";
        using var mode = await Command(client, brief + "/campaign-mode:select", new SelectCampaignModeCommand(
            MasterDataCodes.CampaignModes.OohOnly, MasterDataCodes.CampaignModeDecisionSources.HumanSelection, 1m, "Synthetic scope"));
        using var audience = await Command(client, brief + "/audiences:generate", new { });
        var audienceSetId = audience.RootElement.GetProperty("id").GetGuid();
        using var approvedAudience = await Command(client,
            $"{prefix}/audience-strategies/{audienceSetId}:approve", new
            {
                targetAudienceIds = audience.RootElement.GetProperty("targetAudienceIds")
                    .EnumerateArray().Select(item => item.GetGuid()).ToArray(),
                targetingRationale = audience.RootElement.GetProperty("targetingRationale").GetString(),
                positioningStatement = audience.RootElement.GetProperty("positioningStatement").GetString(),
                reason = "Human reviewed the generated audience strategy.",
            });
        using var mix = await Command(client, brief + "/media-mixes:generate", new { });
        var mixPath = $"{prefix}/media-mix-versions/{mix.RootElement.GetProperty("id").GetGuid()}";
        var purchase = new InventoryPurchaseQuantity(CanonicalPlanningAcceptanceTests.TenantId,
            Guid.Parse("77000000-0000-0000-0000-000000000001"),
            Guid.Parse("78000000-0000-0000-0000-000000000001"), rate, MasterDataCodes.RateTypes.Cpm, 100000);
        var allocation = new MediaAllocationInput(MasterDataCodes.Channels.Ooh, 10000000, "Synthetic CPM purchase",
            [new(new(2026, 9, 1), new(2026, 9, 30))], [purchase]);
        using var updated = await Command(client, mixPath + ":update", new UpdateMediaMixCommand([allocation], "Known quantity"));
        Assert.Equal(1000, updated.RootElement.GetProperty("allocations")[0].GetProperty("purchases")[0].GetProperty("denominator").GetInt32());
        using var approved = await Command(client, mixPath + ":approve", new ApproveMediaMixCommand("Human approval"), 2);
        using var shortlist = await Command(client, brief + "/shortlists:generate", new GenerateShortlistCommand());
        var candidate = shortlist.RootElement.GetProperty("candidates").EnumerateArray()
            .Single(item => item.GetProperty("rateId").GetGuid() == rate);
        Assert.True(candidate.GetProperty("isEligible").GetBoolean());
        using var selected = await Command(client,
            $"{prefix}/shortlist-versions/{shortlist.RootElement.GetProperty("id").GetGuid()}:select",
            new SelectShortlistCommand([candidate.GetProperty("id").GetGuid()], "Human selection"));
        using var planJson = await Command(client, brief + "/media-plans:generate", new GenerateMediaPlanCommand());
        var plan = planJson.Deserialize<MediaPlanVersionView>(WebJson)!;
        var line = Assert.Single(plan.Lines);
        Assert.Equal(100000, line.Quantity);
        Assert.Equal(1000, line.Purchase!.Denominator);
        Assert.Equal(1207500, plan.TotalMinor);
        var snapshot = ProposalPlanProjection.ToSnapshot(plan);
        Assert.Equal(line.Purchase, Assert.Single(snapshot.Inventory).Purchase);
    }

    private static Task<JsonDocument> Command<T>(HttpClient client, string path, T body, long version = 1) =>
        CanonicalPlanningAcceptanceTests.CommandAsync(client, path, Guid.NewGuid().ToString(), version, body);

    private static string Connection()
    {
        var database = Environment.GetEnvironmentVariable("PGDATABASE") ?? "";
        Assert.StartsWith("advertified_brief_test_", database);
        return new NpgsqlConnectionStringBuilder { Host = Environment.GetEnvironmentVariable("PGHOST"), Database = database,
            Username = Environment.GetEnvironmentVariable("PGUSER"), Password = Environment.GetEnvironmentVariable("PGPASSWORD") }.ConnectionString;
    }

    private static async Task<Guid> InsertRateAsync(GovernanceDbContext db)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_rates (id, tenant_id, product_version_id, rate_type_code,
                currency_code, amount_minor, effective_from, effective_to, source_locator, vat_treatment_code, commercial_terms_json)
            SELECT {id}, tenant_id, product_version_id, {MasterDataCodes.RateTypes.Cpm}, currency_code, 10000,
                DATE '2026-08-01', DATE '2026-12-31', 'synthetic:cpm-rate', vat_treatment_code, commercial_terms_json
            FROM commercial.inventory_rates
            WHERE tenant_id = {CanonicalPlanningAcceptanceTests.TenantId}
                AND product_version_id = {Guid.Parse("78000000-0000-0000-0000-000000000001")};
            """);
        return id;
    }
}
