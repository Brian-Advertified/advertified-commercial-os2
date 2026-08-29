using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityGate4AcceptanceTests
{
    private static async Task AssertSuppliedBriefPathAsync(
        HttpClient solo,
        Guid clientId)
    {
        const string original =
            "The client wants qualified Gauteng enquiries by December. Budget was not supplied.";
        using var created = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs",
            "gate5-supplied-brief",
            new
            {
                clientId,
                title = "Solo agency supplied Brief",
                ownerUserId = SoloOperatorId,
                sourceLocator = "supplied:test:brief-1",
                sourceTitle = "Client email pasted by the operator",
                sourceContent = original,
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = await ReadJsonAsync(created);
        var briefId = createdJson.RootElement.GetProperty("id").GetGuid();
        using var draftResponse = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}/versions",
            "gate5-supplied-version-1",
            SuppliedVersion(briefId, null, "Generate qualified enquiries."));
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        using var draftJson = await ReadJsonAsync(draftResponse);
        var draft = draftJson.RootElement.Clone();
        var approved = await ConfirmBriefAsync(
            solo, solo, draft, null, "gate5-solo");

        using var revisionResponse = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}/versions",
            "gate5-supplied-version-2",
            SuppliedVersion(briefId, approved.GetProperty("id").GetGuid(),
                "Generate qualified enquiries and record their source."));
        Assert.Equal(HttpStatusCode.Created, revisionResponse.StatusCode);
        using var detailResponse = await solo.GetAsync(
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}");
        using var detailJson = await ReadJsonAsync(detailResponse);
        var detail = detailJson.RootElement;
        var suppliedSource = detail.GetProperty("sources")[0];
        Assert.Equal(original, suppliedSource.GetProperty("content").GetString());
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(original))),
            suppliedSource.GetProperty("contentHash").GetString());
        Assert.Equal(2, detail.GetProperty("versions").GetArrayLength());
        Assert.Equal(
            approved.GetProperty("id").GetGuid(),
            detail.GetProperty("brief").GetProperty("approvedVersionId").GetGuid());
        Assert.Equal("DRAFT", detail.GetProperty("brief").GetProperty("status").GetString());
    }

    private static async Task AssertOpportunityBriefPathAsync(
        HttpClient owner,
        HttpClient agencyOperator,
        HttpClient advertiser,
        Guid opportunityId,
        string connectionString)
    {
        await QueueAsync(owner, opportunityId, "briefs:draft", "gate5-opportunity-brief", new { });
        var opportunity = await WaitForAsync(
            owner, opportunityId,
            value => value.GetProperty("briefId").ValueKind == JsonValueKind.String);
        var briefId = opportunity.GetProperty("briefId").GetGuid();
        using var detailResponse = await owner.GetAsync(
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}");
        using var detailJson = await ReadJsonAsync(detailResponse);
        var draft = detailJson.RootElement.GetProperty("versions")[0].Clone();
        Assert.Equal("DRAFT", draft.GetProperty("status").GetString());
        Assert.Contains(
            draft.GetProperty("unknowns").EnumerateArray(),
            item => item.GetProperty("fieldPath").GetString() == "budget");
        await ConfirmBriefAsync(
            owner, agencyOperator, draft, SoloOperatorId, "gate5-opportunity", advertiser);

        var final = await GetOpportunityAsync(owner, opportunityId);
        Assert.Equal("PLANNING", final.GetProperty("opportunity").GetProperty("stage").GetString());
        Assert.Equal("The Brief is confirmed and ready for planning.",
            final.GetProperty("nextAction").GetString());
        await AssertGate5LineageAsync(connectionString, opportunityId);
    }

    private static async Task<JsonElement> ConfirmBriefAsync(
        HttpClient submitter,
        HttpClient confirmer,
        JsonElement draft,
        Guid? confirmerId,
        string keyPrefix,
        HttpClient? excludedAdvertiser = null)
    {
        var versionId = draft.GetProperty("id").GetGuid();
        using var submitted = await SendCommandAsync(
            submitter,
            $"/api/v1/tenants/{TenantId}/brief-versions/{versionId}:submit",
            $"{keyPrefix}-submit",
            new { confirmerUserId = confirmerId, comment = "Ready for confirmation." },
            draft.GetProperty("version").GetInt64());
        submitted.EnsureSuccessStatusCode();
        using var submittedJson = await ReadJsonAsync(submitted);
        if (excludedAdvertiser is not null)
        {
            using var excluded = await SendCommandAsync(
                excludedAdvertiser,
                $"/api/v1/tenants/{TenantId}/brief-versions/{versionId}:approve",
                $"{keyPrefix}-advertiser-denied",
                new { reason = "An advertiser must not confirm an agency Brief." },
                submittedJson.RootElement.GetProperty("version").GetInt64());
            Assert.Equal(HttpStatusCode.Forbidden, excluded.StatusCode);
        }
        using var confirmed = await SendCommandAsync(
            confirmer,
            $"/api/v1/tenants/{TenantId}/brief-versions/{versionId}:approve",
            $"{keyPrefix}-confirm",
            new { reason = "Confirmed for planning." },
            submittedJson.RootElement.GetProperty("version").GetInt64());
        confirmed.EnsureSuccessStatusCode();
        using var confirmedJson = await ReadJsonAsync(confirmed);
        Assert.Equal("APPROVED", confirmedJson.RootElement.GetProperty("status").GetString());
        return confirmedJson.RootElement.Clone();
    }

    private static object SuppliedVersion(Guid briefId, Guid? baseVersionId, string objective) => new
    {
        briefId,
        baseVersionId,
        businessProblem = "Qualified enquiry demand is not established.",
        objective,
        audiences = new[] { "People seeking workspace furniture" },
        geographies = new[] { "Gauteng" },
        timing = "By December 2026",
        budgetMinor = (long?)null,
        budgetUnknown = true,
        currency = (string?)null,
        vatStatus = (string?)null,
        feesMinor = (long?)null,
        constraints = Array.Empty<string>(),
        measurement = new[] { "Qualified enquiries" },
        facts = new[] { "The supplied source names Gauteng and December." },
        unknowns = new[]
        {
            new { fieldPath = "budget", question = "What budget is available?", isBlocking = false },
        },
        assumptions = Array.Empty<object>(),
        conflicts = Array.Empty<object>(),
        evidenceItemIds = Array.Empty<Guid>(),
    };

    private static async Task AssertGate5LineageAsync(
        string connectionString,
        Guid opportunityId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(4, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.agent_runs WHERE opportunity_id = $1",
            opportunityId));
        Assert.Equal(5, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.agent_run_steps step " +
            "JOIN commercial.agent_runs run ON run.id = step.run_id " +
            "WHERE run.opportunity_id = $1 AND step.status_code = 'COMPLETED'",
            opportunityId));
        Assert.Equal(1, await ScalarAsync(connection,
            "SELECT count(*)::integer FROM commercial.campaign_briefs " +
            "WHERE opportunity_id = $1 AND status_code = 'APPROVED'",
            opportunityId));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COALESCE(sum(usage.incremental_cost_minor), 0)::integer " +
            "FROM commercial.ai_usage_ledger usage JOIN commercial.agent_runs run " +
            "ON run.id = usage.run_id WHERE run.opportunity_id = $1",
            opportunityId));
    }
}
