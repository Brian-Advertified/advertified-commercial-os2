using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Infrastructure.Brief;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private sealed record ScenarioProductShape(string Channel, string ProductType, string Deliverable)
    {
        internal const string StaticDeliverable =
            """{"format":"Static billboard","buyingUnit":"site/month","dimensions":"3m x 6m","placement":"Roadside","quantity":1}""";
    }

    private static readonly string[] MultiScenarioChannels = ["OOH", "DOOH", "RADIO", "SOCIAL"];
    private static readonly string[] DigitalScenarioChannels = ["DOOH"];
    private static readonly string[] OohScenarioChannels = ["OOH"];

    private static MediaAllocationView[] ScenarioAllocations(string id, long budget)
    {
        var channels = id == "PLAN-002" ? MultiScenarioChannels :
            id == "PLAN-008" ? DigitalScenarioChannels : OohScenarioChannels;
        return channels.Select(channel => new MediaAllocationView(channel, budget / channels.Length,
            "Human-approved synthetic channel role",
            [new(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))])).ToArray();
    }

    private static async Task SeedCoverageScenarioAsync(string connectionString, string id)
    {
        if (id == "PLAN-007")
        {
            await SeedCollectiveGeographyAsync(connectionString);
            return;
        }
        if (id is not ("PLAN-002" or "PLAN-008")) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var channels = id == "PLAN-002" ? MultiScenarioChannels : DigitalScenarioChannels;
        for (var index = 0; index < channels.Length; index++)
        {
            var channel = channels[index];
            var productType = channel switch
            {
                "DOOH" => "DOOH_SCREEN", "RADIO" => "RADIO_SPOT",
                "SOCIAL" => "SOCIAL_PLACEMENT", _ => "OOH_SITE",
            };
            var deliverable = JsonSerializer.Serialize(new
            {
                format = channel + " synthetic placement", buyingUnit = "placement/month",
                dimensions = "3m x 6m", placement = "Synthetic", quantity = 1,
                spotLengthSeconds = id == "PLAN-008" ? 30 : 10,
                slotLengthSeconds = 15, loopLengthSeconds = 60, playsPerLoop = 1,
            });
            await InsertProductAsync(connection, index, 100_000, shape: new(channel, productType, deliverable));
        }
    }

    private static async Task SeedCollectiveGeographyAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new GovernanceDbContext(options);
        // Two pre-approved synthetic requirements in the initial fixture, not a business-step patch.
        BriefSpatialRequirementInput[] inputs =
        [
            new("POINT_RADIUS", "REQUIRED", "Johannesburg radius",
                """{"type":"Point","coordinates":[28.0473,-26.2041]}""", 500,
                SourceLocator: "fixture:approved-johannesburg", IsVerified: true),
            new("POINT_RADIUS", "REQUIRED", "Cape Town radius",
                """{"type":"Point","coordinates":[18.4241,-33.9249]}""", 500,
                SourceLocator: "fixture:approved-cape-town", IsVerified: true),
        ];
        await BriefSpatialRequirements.InsertAsync(db, TenantId, BriefVersionId, OperatorId, Now,
            BriefSpatialRequirements.Normalize(inputs), CancellationToken.None);
    }
}
