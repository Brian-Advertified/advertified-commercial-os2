using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class OpportunityAcceptanceTests
{
    private static async Task AssertSuppliedBriefPathAsync(
        HttpClient solo,
        Guid clientId,
        string connectionString)
    {
        const string original =
            "The client wants qualified Gauteng enquiries by December. Budget was not supplied.";
        using var created = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs",
            "brief-supplied-brief",
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
        Assert.Equal("Opportunity Test Client",
            createdJson.RootElement.GetProperty("clientName").GetString());
        var briefId = createdJson.RootElement.GetProperty("id").GetGuid();
        using var draftResponse = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}/versions",
            "brief-supplied-version-1",
            SuppliedVersion(briefId, null, "Generate qualified enquiries."));
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        using var draftJson = await ReadJsonAsync(draftResponse);
        var draft = draftJson.RootElement.Clone();
        var ready = await MarkBriefReadyAsync(solo, draft, "brief-solo");
        await AssertBriefReadyWithoutApprovalAsync(
            connectionString, briefId, ready.GetProperty("id").GetGuid());

        using var revisionResponse = await SendCommandAsync(
            solo,
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}/versions",
            "brief-supplied-version-2",
            SuppliedVersion(briefId, ready.GetProperty("id").GetGuid(),
                "Generate qualified enquiries and record their source."));
        Assert.Equal(HttpStatusCode.Created, revisionResponse.StatusCode);
        using var detailResponse = await solo.GetAsync(
            $"/api/v1/tenants/{TenantId}/briefs/{briefId}");
        using var detailJson = await ReadJsonAsync(detailResponse);
        var detail = detailJson.RootElement;
        Assert.Equal("Opportunity Test Client",
            detail.GetProperty("brief").GetProperty("clientName").GetString());
        var suppliedSource = detail.GetProperty("sources")[0];
        Assert.Equal(original, suppliedSource.GetProperty("content").GetString());
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(original))),
            suppliedSource.GetProperty("contentHash").GetString());
        Assert.Equal(2, detail.GetProperty("versions").GetArrayLength());
        Assert.Equal(
            ready.GetProperty("id").GetGuid(),
            detail.GetProperty("brief").GetProperty("readyVersionId").GetGuid());
        Assert.Equal(JsonValueKind.Null,
            detail.GetProperty("brief").GetProperty("approvedVersionId").ValueKind);
        Assert.Equal("DRAFT", detail.GetProperty("brief").GetProperty("status").GetString());
    }

    private static async Task AssertOpportunityBriefPathAsync(
        HttpClient owner,
        HttpClient agencyOperator,
        HttpClient advertiser,
        Guid opportunityId,
        string connectionString)
    {
        await QueueAsync(owner, opportunityId, "briefs:draft", "brief-opportunity-brief", new { });
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
            owner, agencyOperator, draft, SoloOperatorId, "brief-opportunity", advertiser);

        var final = await GetOpportunityAsync(owner, opportunityId);
        Assert.Equal("PLANNING", final.GetProperty("opportunity").GetProperty("stage").GetString());
        Assert.Equal("The Brief is confirmed and ready for planning.",
            final.GetProperty("nextAction").GetString());
        await AssertBriefLineageAsync(connectionString, opportunityId);
    }

    private static async Task<JsonElement> MarkBriefReadyAsync(
        HttpClient client,
        JsonElement draft,
        string keyPrefix)
    {
        var versionId = draft.GetProperty("id").GetGuid();
        using var response = await SendCommandAsync(
            client,
            $"/api/v1/tenants/{TenantId}/brief-versions/{versionId}:ready",
            $"{keyPrefix}-ready",
            new { },
            draft.GetProperty("version").GetInt64());
        response.EnsureSuccessStatusCode();
        using var json = await ReadJsonAsync(response);
        Assert.Equal("READY", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("approvedBy").ValueKind);
        return json.RootElement.Clone();
    }

    private static async Task AssertBriefReadyWithoutApprovalAsync(
        string connectionString,
        Guid briefId,
        Guid versionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await ScalarAsync(
            connection,
            "SELECT count(*)::integer FROM commercial.campaign_briefs " +
            "WHERE id = $1 AND status_code = 'READY' " +
            "AND ready_version_id IS NOT NULL AND approved_version_id IS NULL",
            briefId));
        Assert.Equal(0, await ScalarAsync(
            connection,
            "SELECT count(*)::integer FROM commercial.human_tasks " +
            "WHERE resource_id = $1 AND task_type_code = 'BRIEF_APPROVAL'",
            versionId));
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
        budgetMinor = (long?)10_000_000,
        budgetUnknown = false,
        currency = "ZAR",
        vatStatus = (string?)null,
        feesMinor = (long?)null,
        constraints = Array.Empty<string>(),
        measurement = new[] { "Qualified enquiries" },
        facts = new[] { "The supplied source names Gauteng, December and a media budget." },
        unknowns = Array.Empty<object>(),
        assumptions = Array.Empty<object>(),
        conflicts = Array.Empty<object>(),
        evidenceItemIds = Array.Empty<Guid>(),
    };

    private static async Task AssertBriefLineageAsync(
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
