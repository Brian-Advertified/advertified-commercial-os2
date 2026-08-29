using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityGate4AcceptanceTests
{
    private static async Task<Guid> CreateClientAsync(HttpClient owner)
    {
        using var response = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/client-accounts",
            "gate4-client",
            new
            {
                externalReference = "gate4-client",
                legalName = "Gate Four Client (Pty) Ltd",
                tradingName = "Gate Four Client",
                website = "https://client.example",
                industry = "Workspace furniture",
                billingProfileJson = "{}",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreateOpportunityAsync(
        HttpClient owner,
        Guid clientId)
    {
        using var response = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities",
            "gate4-opportunity",
            new
            {
                clientId,
                title = "Gauteng workspace furniture growth",
                sourceType = "DISCOVERY",
                sourceRef = "local-qualification",
                ownerUserId = OwnerId,
                expectedValueMinor = 500_000L,
                currency = "ZAR",
                deadline = new DateOnly(2026, 12, 1),
                problemSummary = "Qualified demand is not yet documented.",
                objectiveSummary = "Create evidence-backed qualified enquiries.",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        return json.RootElement.Clone();
    }

    private static async Task RegisterEvidenceAsync(HttpClient owner, Guid opportunityId)
    {
        using var response = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/evidence-sources",
            "gate4-source",
            new
            {
                opportunityId,
                type = "SUPPLIED_TEXT",
                locator = "supplied:qualification:1",
                title = "Owner-supplied qualification notes",
                policyBasis = "OWNER_SUPPLIED",
                content = "The client supplies modular workspace furniture to small Gauteng teams.",
                reviewerUserId = ReviewerId,
                claims = new[]
                {
                    new
                    {
                        locator = "supplied:qualification:1#claim-1",
                        claimType = "BUSINESS_CONTEXT",
                        structuredValueJson =
                            "{\"statement\":\"Modular furniture for small Gauteng teams\"}",
                        excerpt =
                            "The client supplies modular workspace furniture to small Gauteng teams.",
                        confidence = 1m,
                    },
                },
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AssertCaptureBoundaryAsync(HttpClient owner, Guid opportunityId)
    {
        var body = new
        {
            opportunityId,
            type = "PERMITTED_URL",
            title = "Blocked external source",
            policyBasis = "OWNER_PERMITTED",
            content = (string?)null,
            reviewerUserId = ReviewerId,
            claims = Array.Empty<object>(),
        };
        using var unsafeUrl = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/evidence-sources",
            "gate4-unsafe-url",
            new { body.opportunityId, body.type, locator = "https://user@fixture.local/source",
                body.title, body.policyBasis, body.content, body.reviewerUserId, body.claims });
        await AssertProblemAsync(unsafeUrl, HttpStatusCode.BadRequest, "VALIDATION_FAILED");

        using var disabledProvider = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/evidence-sources",
            "gate4-disabled-provider",
            new { body.opportunityId, body.type, locator = "https://example.invalid/source",
                body.title, body.policyBasis, body.content, body.reviewerUserId, body.claims });
        await AssertProblemAsync(
            disabledProvider, HttpStatusCode.Conflict, "CAPTURE_PROVIDER_DISABLED");
    }

    private static Task StartQualificationAsync(
        HttpClient owner,
        Guid opportunityId,
        long version) => SendSuccessfulCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/qualification:start",
            "gate4-start",
            new { comment = "Begin evidence review." },
            version);

    private static Task ReviewEvidenceAsync(HttpClient reviewer, Guid itemId) =>
        SendSuccessfulCommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/evidence-items/{itemId}/review",
            "gate4-review",
            new { decision = "APPROVE", structuredValueJson = (string?)null, reason = (string?)null },
            1);

    private static async Task SubmitAndApproveEvidenceAsync(
        HttpClient owner,
        HttpClient reviewer,
        Guid opportunityId)
    {
        using var submitted = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/evidence:submit",
            "gate4-submit-evidence",
            new { gaps = EvidenceGaps, approverUserId = ReviewerId },
            2);
        submitted.EnsureSuccessStatusCode();
        using var submittedJson = await ReadJsonAsync(submitted);
        var evidenceSet = submittedJson.RootElement;
        using var tasksResponse = await reviewer.GetAsync(
            $"/api/v1/tenants/{TenantId}/human-tasks");
        using var tasksJson = await ReadJsonAsync(tasksResponse);
        var approvalTask = tasksJson.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(task => task.GetProperty("taskType").GetString() == "EVIDENCE_SET_APPROVAL" &&
                task.GetProperty("status").GetString() == "PENDING");
        await SendSuccessfulCommandAsync(
            reviewer,
            $"/api/v1/tenants/{TenantId}/human-tasks/" +
                $"{approvalTask.GetProperty("id").GetGuid()}:complete",
            "gate4-approve-evidence",
            new
            {
                action = "APPROVE",
                selectedResourceId = (Guid?)null,
                decision = (string?)null,
                structuredValueJson = (string?)null,
                resolution = (string?)null,
                reason = "The reviewed claim matches the supplied text.",
            },
            approvalTask.GetProperty("resourceVersion").GetInt64());
        Assert.Equal(
            evidenceSet.GetProperty("id").GetGuid(),
            approvalTask.GetProperty("resourceId").GetGuid());
    }

    private static async Task QueueAsync<T>(
        HttpClient owner,
        Guid opportunityId,
        string action,
        string key,
        T body)
    {
        using var response = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/opportunities/{opportunityId}/{action}",
            key,
            body);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static async Task ResolveSubmitAndApproveStrategyAsync(
        HttpClient owner,
        HttpClient approver,
        JsonElement strategy,
        JsonElement objection,
        Guid opportunityId)
    {
        await SendSuccessfulCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/critic-objections/" +
                $"{objection.GetProperty("id").GetGuid()}:resolve",
            "gate4-resolve-objection",
            new { resolution = "ADDRESSED", reason = "Baseline remains unknown and is a task." },
            objection.GetProperty("version").GetInt64());
        var strategyId = strategy.GetProperty("id").GetGuid();
        using var submitted = await SendCommandAsync(
            owner,
            $"/api/v1/tenants/{TenantId}/strategy-versions/{strategyId}:submit",
            "gate4-submit-strategy",
            new { comment = "Submit the evidence-bound strategy." },
            strategy.GetProperty("version").GetInt64());
        submitted.EnsureSuccessStatusCode();
        using var submittedJson = await ReadJsonAsync(submitted);
        await SendSuccessfulCommandAsync(
            approver,
            $"/api/v1/tenants/{TenantId}/strategy-versions/{strategyId}:approve",
            "gate4-approve-strategy",
            new { reason = "Approved for Brief drafting." },
            submittedJson.RootElement.GetProperty("version").GetInt64());

        var final = await GetOpportunityAsync(owner, opportunityId);
        Assert.Equal("APPROVED", final.GetProperty("strategy").GetProperty("status").GetString());
    }
}
