using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    public static IEnumerable<object[]> BackendOpportunityCases() =>
        BackendScenarioEvidence.Cases("OPPORTUNITY_DISCOVERY");

    [Theory]
    [MemberData(nameof(BackendOpportunityCases))]
    [Trait("Category", "Migration")]
    public Task CanonicalOpportunityScenario(string scenarioId) =>
        BackendScenarioEvidence.RunAsync(scenarioId, () => scenarioId == "OPP-005"
            ? RunDuplicateOpportunityScenarioAsync() : RunOpportunityScenarioAsync(scenarioId));

    private static async Task<BackendScenarioObservation> RunOpportunityScenarioAsync(string id)
    {
        await using var postgres = CreatePostgres();
        await postgres.StartAsync();
        var connection = postgres.GetConnectionString();
        await DisposableDatabaseRoles.ProvisionAsync(connection);
        await SeedAsync(connection);
        await using var ownerFactory = CreateFactory(connection, OwnerId, enableRuntime: true);
        await using var reviewerFactory = CreateFactory(connection, ReviewerId);
        using var owner = ownerFactory.CreateClient();
        using var reviewer = reviewerFactory.CreateClient();
        var clientId = await CreateClientAsync(owner);
        var definition = BackendScenarioEvidence.Definition(id);
        var source = ScenarioOpportunitySource(id);
        var body = new
        {
            clientId, title = definition.GetProperty("name").GetString(),
            sourceType = "DISCOVERY", sourceRef = "synthetic:" + id, ownerUserId = OwnerId,
            expectedValueMinor = (long?)null, currency = (string?)null, deadline = (DateOnly?)null,
            problemSummary = source, objectiveSummary = "Evaluate supplied evidence before any client request.",
        };
        using var created = await SendCommandAsync(owner,
            $"/api/v1/tenants/{TenantId}/opportunities", id + ":create", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await ReadJsonAsync(created);
        var opportunityId = createdJson.RootElement.GetProperty("id").GetGuid();
        using var forbidden = await owner.GetAsync($"/api/v1/tenants/{OtherTenantId}/opportunities/{opportunityId}");
        await AssertProblemAsync(forbidden, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        var approved = id is "OPP-001" or "OPP-004" or "OPP-007" or "OPP-008" or "OPP-009";
        await ExerciseScenarioEvidenceAsync(id, owner, reviewer, opportunityId, source, approved);
        var detail = await GetOpportunityAsync(owner, opportunityId);
        Assert.Equal("DISCOVERY", detail.GetProperty("opportunity").GetProperty("sourceType").GetString());
        using var briefsResponse = await owner.GetAsync($"/api/v1/tenants/{TenantId}/briefs");
        using var briefs = await ReadJsonAsync(briefsResponse);
        Assert.Empty(briefs.RootElement.EnumerateArray());
        return new(new { body, evidenceContent = source },
            new { detail, noClientBriefCreated = true, crossTenantStatus = (int)forbidden.StatusCode },
            detail.GetProperty("opportunity").GetProperty("stage").GetString()!,
            new Dictionary<string, bool>
            {
                ["EVIDENCE_REQUIRED_FOR_ASSERTION"] = true,
                ["SUPPLIED_BRIEF_REMAINS_DISTINCT"] = true,
                ["NO_FAKE_CLIENT_REQUEST"] = true,
            }, approved ? ["INTERPRETATION_CONFIRMATION", "ANGLE_SELECTION"] : ["EVIDENCE_REVIEW"],
            0, approved ? 0 : 1, "NO_COMMERCIAL_COMMITMENT", "CROSS_TENANT_READ_REJECTED");
    }

    private static string ScenarioOpportunitySource(string id) => id switch
    {
        "OPP-001" => "Synthetic approved business supplies modular furniture to Gauteng teams.",
        "OPP-002" => "Hypothesis only: a service may be useful; demand is unverified.",
        "OPP-003" => "No evidence supplied.",
        "OPP-004" => "Synthetic offering supports both local discovery and enquiry follow-up.",
        "OPP-006" => "Archived 2011 signal; current commercial relevance is unverified.",
        "OPP-007" => "Existing synthetic client is considering a new enquiry objective; no new Brief was supplied.",
        "OPP-008" => "Supplier reports a changed synthetic placement offering; demand remains unknown.",
        "OPP-009" => "Synthetic business proposes a move from Gauteng to Limpopo; demand remains unknown.",
        "OPP-010" => "Non-actionable hypothesis without current demand or an authorised client request.",
        _ => throw new ArgumentException("No opportunity source fixture exists.", nameof(id)),
    };
}
