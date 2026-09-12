using System.Net;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class CanonicalPlanningAcceptanceTests
{
    private static async Task SeedMissingSupplyScenarioAsync(string connectionString, string id)
    {
        if (id is not ("PLAN-005" or "PLAN-006")) return;
        // Initial isolated fixture only: all subsequent actions use canonical commands.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        if (id == "PLAN-005") await InsertProductAsync(connection, 0, 100_000, "UNAVAILABLE");
        else await InsertProductAsync(connection, 0, null);
    }

    private static async Task<object> AssertMissingSupplyScenarioAsync(
        HttpClient client, string id, JsonElement shortlist, JsonElement[] candidates)
    {
        var candidate = Assert.Single(candidates);
        Assert.False(candidate.GetProperty("isEligible").GetBoolean());
        var expected = id == "PLAN-005" ? "UNAVAILABLE" : "MISSING_INFO";
        Assert.Equal(expected, candidate.GetProperty("rejectionReason").GetString());
        Assert.Empty(shortlist.GetProperty("campaignCombinations").GetProperty("alternatives").EnumerateArray());
        if (id == "PLAN-006")
        {
            Assert.Equal(JsonValueKind.Null, candidate.GetProperty("rateId").ValueKind);
            Assert.Equal(JsonValueKind.Null, candidate.GetProperty("rateAmountMinor").ValueKind);
        }
        using var rejected = await RawCommandAsync(client,
            Path($"shortlist-versions/{shortlist.GetProperty("id").GetGuid()}:select"), id + ":invalid-select", 1,
            new { selectedCandidateIds = new[] { candidate.GetProperty("id").GetGuid() }, reason = "Must reject ineligible supply." });
        await AssertProblemAsync(rejected, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        return new { candidate, noAlternativeOffered = true, rejection = "INVALID_LIFECYCLE_TRANSITION" };
    }
}
