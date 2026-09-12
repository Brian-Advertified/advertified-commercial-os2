using System.Net;
using Advertified.Commercial.Infrastructure.MasterData;
using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Commercial;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    private static async Task<BackendScenarioObservation> RunDuplicateOpportunityScenarioAsync()
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        await SeedDuplicateTenantAccessAsync(connection);
        await using var factory = CreateFactory(connection, OwnerId);
        using var owner = factory.CreateClient();
        var clientId = await CreateClientAsync(owner);
        var path = $"/api/v1/tenants/{TenantId}/opportunities";
        var body = DuplicateBody(clientId, " discovery ", " source:exact ");
        using var created = await SendCommandAsync(owner, path, "duplicate:original", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var json = await ReadJsonAsync(created);
        var originalId = json.RootElement.GetProperty("id").GetGuid();
        using var duplicate = await SendCommandAsync(owner, path, "duplicate:new",
            DuplicateBody(clientId, "DISCOVERY", "source:exact"));
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "DUPLICATE_OPPORTUNITY");
        using var replay = await SendCommandAsync(owner, path, "duplicate:original", body);
        Assert.True(replay.IsSuccessStatusCode);
        using var replayJson = await ReadJsonAsync(replay);
        Assert.Equal(originalId, replayJson.RootElement.GetProperty("id").GetGuid());
        using var noEvidence = await SendCommandAsync(owner,
            path + $"/{originalId}/qualification:start", "duplicate:qualify", new { comment = "No source evidence." }, 1);
        await AssertProblemAsync(noEvidence, HttpStatusCode.Conflict, "EVIDENCE_REQUIRED");
        var concurrent = await AssertConcurrentDuplicateAsync(owner, clientId);
        var distinct = await AssertDistinctOpportunityIdentitiesAsync(owner, clientId);
        using var forbidden = await owner.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/opportunities/{originalId}");
        await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var list = await owner.GetAsync(path);
        using var rows = await ReadJsonAsync(list);
        Assert.Equal(8, rows.RootElement.GetProperty("items").GetArrayLength());
        return new(new { body, concurrentSourceRef = "source:concurrent" },
            new { originalId, duplicateStatus = 409, replayId = originalId, concurrent,
                distinct, opportunityCount = rows.RootElement.GetProperty("items").GetArrayLength(), crossTenantStatus = 403 },
            "EXACT_DUPLICATE_REJECTED",
            new Dictionary<string, bool>
            {
                ["EVIDENCE_REQUIRED_FOR_ASSERTION"] = true,
                ["SUPPLIED_BRIEF_REMAINS_DISTINCT"] = true,
                ["NO_FAKE_CLIENT_REQUEST"] = true,
            }, ["EXISTING_OPPORTUNITY_REVIEW"], 0, 0,
            "NO_COMMERCIAL_COMMITMENT", "TENANT_IDENTITY_DISTINCT_AND_CROSS_TENANT_RECORD_HIDDEN");
    }

    private static object DuplicateBody(Guid clientId, string sourceType, string? sourceRef) => new
    {
        clientId, title = "Synthetic exact duplicate acceptance", sourceType, sourceRef,
        ownerUserId = OwnerId, expectedValueMinor = (long?)null, currency = (string?)null,
        deadline = (DateOnly?)null, problemSummary = "No evidence supplied.",
        objectiveSummary = "Review source before qualification.",
    };

    private static async Task<int[]> AssertConcurrentDuplicateAsync(HttpClient owner, Guid clientId)
    {
        var body = DuplicateBody(clientId, "DISCOVERY", "source:concurrent");
        var path = $"/api/v1/tenants/{TenantId}/opportunities";
        var responses = await Task.WhenAll(
            SendCommandAsync(owner, path, "duplicate:concurrent:1", body),
            SendCommandAsync(owner, path, "duplicate:concurrent:2", body));
        try
        {
            var statuses = responses.Select(item => (int)item.StatusCode).Order().ToArray();
            Assert.Equal(201, statuses[0]);
            Assert.Equal(409, statuses[1]);
            await AssertProblemAsync(responses.Single(item => item.StatusCode == HttpStatusCode.Conflict),
                HttpStatusCode.Conflict, "DUPLICATE_OPPORTUNITY");
            return statuses;
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    private static async Task SeedDuplicateTenantAccessAsync(string connection)
    {
        var options = new DbContextOptionsBuilder<GovernanceDbContext>().UseNpgsql(connection).Options;
        await using var db = new GovernanceDbContext(options);
        db.Memberships.Add(new Membership(
            new MembershipId(Guid.NewGuid()), new TenantId(OtherTenantId), new UserId(OwnerId),
            new RoleCode("platform_admin"), new LifecycleStatusCode("ACTIVE"), null, Now));
        await db.SaveChangesAsync();
    }
}
