using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    [Fact]
    [Trait("Category", "Migration")]
    public async Task InventoryIntelligenceIsAdvisoryVersionedAndIdempotent()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connectionString);
        await SeedAsync(connectionString);
        await using var factory = CreateFactory(
            connectionString, OperatorId, configureServices: ConfigureDeterministicPlanningClock);
        using var client = factory.CreateClient();

        using var mode = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/campaign-mode:select"),
            "inventory-intelligence-mode", 1, Mode("OOH_ONLY"));
        using var audience = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/audiences:generate"),
            "inventory-intelligence-audience", 1, new { });
        var audienceId = audience.RootElement.GetProperty("id").GetGuid();
        using var approvedAudience = await CommandAsync(
            client, Path($"audience-strategies/{audienceId}:approve"),
            "inventory-intelligence-audience-approve", 1, new
            {
                targetAudienceIds = audience.RootElement.GetProperty("targetAudienceIds")
                    .EnumerateArray().Select(item => item.GetGuid()).ToArray(),
                targetingRationale = audience.RootElement.GetProperty("targetingRationale").GetString(),
                positioningStatement = audience.RootElement.GetProperty("positioningStatement").GetString(),
                reason = "Reviewed target audience for inventory interpretation.",
            });
        using var mediaStrategy = await AnalyseAndApproveMediaStrategyAsync(client, BriefVersionId);
        using var mix = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/media-mixes:generate"),
            "inventory-intelligence-mix", 1, new { });
        var mixId = mix.RootElement.GetProperty("id").GetGuid();
        using var editedMix = await CommandAsync(
            client, Path($"media-mix-versions/{mixId}:update"),
            "inventory-intelligence-mix-edit", 1, new
            {
                allocations = new[]
                {
                    new
                    {
                        channel = "OOH",
                        budgetMinor = 1_000_000,
                        role = "Approved local visibility role",
                        runningPeriods = new[]
                        {
                            new { start = "2026-09-01", end = "2026-09-30" },
                        },
                    },
                },
                reason = "Apply the approved September running period.",
            });
        using var approvedMix = await CommandAsync(
            client, Path($"media-mix-versions/{mixId}:approve"),
            "inventory-intelligence-mix-approve", 2, new { reason = "Reviewed media mix." });
        using var shortlist = await CommandAsync(
            client, Path($"brief-versions/{BriefVersionId}/shortlists:generate"),
            "inventory-intelligence-shortlist", 1, new { });
        var shortlistId = shortlist.RootElement.GetProperty("id").GetGuid();

        using var firstResponse = await client.PostAsync(
            Path($"shortlist-versions/{shortlistId}/intelligence/inventory"), null);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();
        Assert.True(firstResponse.IsSuccessStatusCode, firstBody);
        using var first = JsonDocument.Parse(firstBody);
        Assert.Equal("InventoryShortlistVersion", first.RootElement.GetProperty("subjectType").GetString());
        Assert.Equal(shortlistId, first.RootElement.GetProperty("subjectId").GetGuid());
        Assert.Equal("inventory_intelligence", first.RootElement.GetProperty("serviceCode").GetString());
        Assert.Equal("DRAFT", first.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, first.RootElement.GetProperty("totalIncrementalCostMinor").GetInt64());
        using var artifact = JsonDocument.Parse(first.RootElement.GetProperty("artifactJson").GetString()!);
        Assert.Equal(6, artifact.RootElement.GetProperty("interpretations").GetArrayLength());
        Assert.All(artifact.RootElement.GetProperty("interpretations").EnumerateArray(), item =>
            Assert.Equal("AI_RECOMMENDATION", item.GetProperty("classification").GetString()));
        var artifactId = first.RootElement.GetProperty("id").GetGuid();

        using var repeatedResponse = await client.PostAsync(
            Path($"shortlist-versions/{shortlistId}/intelligence/inventory"), null);
        repeatedResponse.EnsureSuccessStatusCode();
        using var repeated = JsonDocument.Parse(await repeatedResponse.Content.ReadAsStringAsync());
        Assert.Equal(artifactId, repeated.RootElement.GetProperty("id").GetGuid());

        using var latestResponse = await client.GetAsync(
            Path($"shortlist-versions/{shortlistId}/intelligence/inventory"));
        latestResponse.EnsureSuccessStatusCode();
        using var latest = JsonDocument.Parse(await latestResponse.Content.ReadAsStringAsync());
        Assert.Equal(artifactId, latest.RootElement.GetProperty("id").GetGuid());
    }
}
