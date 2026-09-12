using System.Net;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    private static async Task ExerciseScenarioEvidenceAsync(
        string id, HttpClient owner, HttpClient reviewer, Guid opportunityId, string source, bool approve)
    {
        if (id == "OPP-003")
        {
            using var absent = await SendCommandAsync(owner,
                $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/qualification:start",
                id + ":qualify", new { comment = "Evidence is absent." }, 1);
            await AssertProblemAsync(absent, HttpStatusCode.Conflict, "EVIDENCE_REQUIRED");
            return;
        }
        await RegisterScenarioSourceAsync(owner, opportunityId, source, id);
        await StartQualificationAsync(owner, opportunityId, 1);
        using var unapproved = await SendCommandAsync(owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/interpret", id + ":premature-interpret", new { });
        await AssertProblemAsync(unapproved, HttpStatusCode.Conflict, "INVALID_LIFECYCLE_TRANSITION");
        if (!approve) return;
        var detail = await GetOpportunityAsync(owner, opportunityId);
        var itemId = detail.GetProperty("evidenceItems")[0].GetProperty("id").GetGuid();
        await ReviewEvidenceAsync(reviewer, itemId);
        await SubmitAndApproveEvidenceAsync(owner, reviewer, opportunityId);
        await QueueAsync(owner, opportunityId, "interpret", id + ":interpret", new { });
        detail = await WaitForAsync(owner, opportunityId,
            value => value.GetProperty("interpretation").ValueKind == JsonValueKind.Object);
        var interpretation = detail.GetProperty("interpretation");
        using var artifact = JsonDocument.Parse(interpretation.GetProperty("artifactJson").GetString()!);
        Assert.Equal(source, artifact.RootElement.GetProperty("offering").GetString());
        await SendSuccessfulCommandAsync(owner,
            $"/api/v1/tenants/{TenantId}/business-interpretations/{interpretation.GetProperty("id").GetGuid()}:confirm",
            id + ":confirm", new { comment = "Reviewed against the exact synthetic source." },
            interpretation.GetProperty("version").GetInt64());
        await QueueAsync(owner, opportunityId, "angles:generate", id + ":angles", new { });
        detail = await WaitForAsync(owner, opportunityId, value => value.GetProperty("angles").GetArrayLength() >= 2);
        Assert.All(detail.GetProperty("angles").EnumerateArray(), angle =>
        {
            using var ids = JsonDocument.Parse(angle.GetProperty("evidenceItemIdsJson").GetString()!);
            Assert.Equal(itemId, Assert.Single(ids.RootElement.EnumerateArray()).GetGuid());
        });
    }

    private static async Task RegisterScenarioSourceAsync(
        HttpClient owner, Guid opportunityId, string source, string id)
    {
        using var response = await SendCommandAsync(owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/evidence-sources", id + ":source",
            new
            {
                opportunityId, type = "SUPPLIED_TEXT", locator = "supplied:scenario:" + id,
                title = "Synthetic scenario evidence", policyBasis = "OWNER_SUPPLIED",
                content = source, reviewerUserId = ReviewerId,
                claims = new[] { new
                {
                    locator = "supplied:scenario:" + id + "#claim",
                    claimType = "BUSINESS_CONTEXT", structuredValueJson = JsonSerializer.Serialize(new { statement = source }),
                    excerpt = source, confidence = 1m,
                } },
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
