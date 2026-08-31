using System.Net;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertMeasurementBlockedWithoutFactsAsync(
        HttpClient buyer,
        Guid campaignId)
    {
        using var response = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-without-facts", null,
            new { approverUserId = ClientUserId });
        await AssertProblemAsync(
            response, HttpStatusCode.Conflict, "MEASUREMENT_REPORT_BLOCKED");
    }

    private static async Task AssertMeasurementReportAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient other,
        Guid campaignId,
        Guid evidenceId,
        string connectionString)
    {
        await AssertMeasurementGenerationGuardsAsync(buyer, other, campaignId);
        using var first = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-first", null, new { approverUserId = ClientUserId });
        var firstId = first.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("REVIEW_REQUIRED", first.RootElement.GetProperty("status").GetString());
        Assert.Equal("NOT_ESTABLISHED",
            first.RootElement.GetProperty("interpretation")
                .GetProperty("causalityStatus").GetString());
        Assert.Equal(evidenceId, Assert.Single(first.RootElement.GetProperty("evidence")
            .EnumerateArray()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("objectKey", first.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("agentRun", first.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        await AssertPendingMeasurementVisibilityAsync(
            buyer, client, other, campaignId, firstId);
        using var duplicatePending = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-pending-exists", null,
            new { approverUserId = ClientUserId });
        await AssertProblemAsync(
            duplicatePending, HttpStatusCode.Conflict, "MEASUREMENT_REPORT_BLOCKED");
        using var wrongReviewer = await RawCommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{firstId}:review",
            "measurement-report-wrong-reviewer", 1,
            new { approved = true, reason = "The generator cannot review this report." });
        await AssertProblemAsync(wrongReviewer, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");

        using var rejected = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{firstId}:review",
            "measurement-report-reject", 1,
            new { approved = false, reason = "The client wording needs a corrected version." });
        Assert.Equal("REJECTED", rejected.RootElement.GetProperty("status").GetString());
        await AssertNoApprovedMeasurementReportsAsync(buyer, campaignId);

        using var second = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-second", null, new { approverUserId = ClientUserId });
        var secondId = second.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(2, second.RootElement.GetProperty("versionNumber").GetInt32());
        using var approved = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{secondId}:review",
            "measurement-report-approve", 1,
            new { approved = true, reason = "The sourced limitations and wording are approved." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        using var repeated = await RawCommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/measurement-reports/{secondId}:review",
            "measurement-report-repeat-review", 2,
            new { approved = false, reason = "An approved report cannot be overwritten." });
        await AssertProblemAsync(
            repeated, HttpStatusCode.Conflict, "MEASUREMENT_REPORT_BLOCKED");
        await AssertApprovedMeasurementProjectionAsync(buyer, campaignId, secondId);
        await AssertMeasurementPersistenceAsync(
            connectionString, campaignId, firstId, secondId);
    }

    private static async Task AssertMeasurementGenerationGuardsAsync(
        HttpClient buyer,
        HttpClient other,
        Guid campaignId)
    {
        using var wrongTenant = await RawCommandAsync(
            other, OtherTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-wrong-tenant", null,
            new { approverUserId = ClientUserId });
        await AssertProblemAsync(wrongTenant, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var selfReview = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/measurement-reports:generate",
            "measurement-report-self-review", null,
            new { approverUserId = BuyerUserId });
        await AssertProblemAsync(
            selfReview, HttpStatusCode.Conflict, "MEASUREMENT_REPORT_BLOCKED");
    }

    private static async Task AssertPendingMeasurementVisibilityAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient other,
        Guid campaignId,
        Guid reportId)
    {
        using var direct = await ReadAsync(
            client, BuyerTenantId, $"measurement-reports/{reportId}");
        Assert.Equal(reportId, direct.RootElement.GetProperty("id").GetGuid());
        using var crossTenant = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/measurement-reports/{reportId}");
        await AssertProblemAsync(crossTenant, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        await AssertNoApprovedMeasurementReportsAsync(buyer, campaignId);
        using var tasks = await ReadAsync(client, BuyerTenantId, "human-tasks");
        var task = Assert.Single(tasks.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("taskType").GetString() ==
                    "MEASUREMENT_REPORT_REVIEW" &&
                item.GetProperty("resourceId").GetGuid() == reportId);
        Assert.Equal(ClientUserId, task.GetProperty("assigneeUserId").GetGuid());
    }

    private static async Task AssertNoApprovedMeasurementReportsAsync(
        HttpClient buyer,
        Guid campaignId)
    {
        using var campaign = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        Assert.Empty(campaign.RootElement.GetProperty("measurementReports").EnumerateArray());
    }

    private static async Task AssertApprovedMeasurementProjectionAsync(
        HttpClient buyer,
        Guid campaignId,
        Guid reportId)
    {
        using var campaign = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        var report = Assert.Single(campaign.RootElement.GetProperty("measurementReports")
            .EnumerateArray());
        Assert.Equal(reportId, report.GetProperty("id").GetGuid());
        Assert.Equal("APPROVED", report.GetProperty("status").GetString());
        Assert.Single(report.GetProperty("interpretation").GetProperty("findings")
            .EnumerateArray());
        Assert.DoesNotContain("agentRun", campaign.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertMeasurementPersistenceAsync(
        string connectionString,
        Guid campaignId,
        Guid rejectedId,
        Guid approvedId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.measurement_report_versions"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.agent_runs " +
            "WHERE campaign_id IS NOT NULL AND run_kind_code = 'MEASUREMENT'"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.ai_usage_ledger usage " +
            "JOIN commercial.agent_runs run ON run.id = usage.run_id " +
            "WHERE run.campaign_id IS NOT NULL AND usage.incremental_cost_minor = 0"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.human_tasks " +
            "WHERE task_type_code = 'MEASUREMENT_REPORT_REVIEW' " +
            "AND status_code = 'COMPLETED'"));
        await AssertMeasurementImmutableAsync(connection, campaignId, rejectedId, approvedId);
    }

    private static async Task AssertMeasurementImmutableAsync(
        NpgsqlConnection connection,
        Guid campaignId,
        Guid rejectedId,
        Guid approvedId)
    {
        await using var mutate = new NpgsqlCommand(
            "UPDATE commercial.measurement_report_versions " +
            "SET interpretation_json = '{}' WHERE id = $1", connection);
        mutate.Parameters.AddWithValue(approvedId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(mutate.ExecuteNonQueryAsync)).SqlState);
        await using var delete = new NpgsqlCommand(
            "DELETE FROM commercial.measurement_report_versions WHERE id = $1", connection);
        delete.Parameters.AddWithValue(rejectedId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(delete.ExecuteNonQueryAsync)).SqlState);
        await using var retained = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.measurement_report_versions " +
            "WHERE campaign_id = $1 AND ((id = $2 AND status_code = 'REJECTED') " +
            "OR (id = $3 AND status_code = 'APPROVED'))", connection);
        retained.Parameters.AddWithValue(campaignId);
        retained.Parameters.AddWithValue(rejectedId);
        retained.Parameters.AddWithValue(approvedId);
        Assert.Equal(2, (int)(await retained.ExecuteScalarAsync())!);
    }
}
