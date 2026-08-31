using System.Net;
using System.Text;
using Npgsql;
using Xunit;

namespace Advertified.Commercial.Api.Tests;

public sealed partial class MarketplaceAcceptanceTests
{
    private static async Task AssertPerformanceBlockedBeforeCompletionAsync(
        HttpClient buyer,
        Guid campaignId)
    {
        using var response = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-before-completion", null,
            PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(),
                "before-completion"));
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    private static async Task AssertPerformanceEvidenceAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient other,
        Guid campaignId,
        string connectionString)
    {
        await AssertPerformanceInputGuardsAsync(buyer, other, campaignId);
        using var submitted = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-submit", null,
            PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(), "verified-source"));
        var evidenceId = submitted.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("SUBMITTED", submitted.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, submitted.RootElement.GetProperty("version").GetInt64());
        Assert.True(submitted.RootElement.GetProperty("signatureValidated").GetBoolean());
        Assert.Equal("CLEAN",
            submitted.RootElement.GetProperty("malwareScanStatus").GetString());
        Assert.DoesNotContain("objectKey", submitted.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        await AssertPerformanceVisibilityAndTaskAsync(
            buyer, client, other, campaignId, evidenceId);
        await AssertPerformanceReviewGuardsAsync(buyer, campaignId, evidenceId);
        using var approved = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{evidenceId}:review",
            "performance-approve", 1,
            new { approved = true, reason = "Source and method match the retained facts." });
        Assert.Equal("APPROVED", approved.RootElement.GetProperty("status").GetString());
        using var repeatedReview = await RawCommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{evidenceId}:review",
            "performance-repeat-review", 2,
            new { approved = false, reason = "A retained approval cannot be overwritten." });
        await AssertProblemAsync(
            repeatedReview, HttpStatusCode.Conflict, "PERFORMANCE_EVIDENCE_BLOCKED");
        var rejectedEvidenceId = await AssertUnusablePerformanceRejectedAsync(
            buyer, client, campaignId);
        await AssertReviewedPerformanceCampaignAsync(
            buyer, campaignId, evidenceId, rejectedEvidenceId);
        await AssertPerformancePersistenceAsync(connectionString, campaignId, evidenceId);
        await AssertUnauthorizedPerformanceInsertAsync(connectionString, campaignId);
        await AssertMeasurementReportAsync(
            buyer, client, other, campaignId, evidenceId, connectionString);
    }

    private static async Task AssertPerformanceInputGuardsAsync(
        HttpClient buyer,
        HttpClient other,
        Guid campaignId)
    {
        using var unrelated = await RawCommandAsync(
            other, OtherTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-wrong-tenant", null,
            PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(), "wrong-tenant"));
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var selfReview = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-self-review", null,
            PerformanceBody(BuyerUserId, "VERIFIED", ValidMetric(), "self-review"));
        await AssertProblemAsync(selfReview, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        var blockedBodies = new (string Key, object Body)[]
        {
            ("no-limitations", PerformanceBody(
                ClientUserId, "VERIFIED", ValidMetric(), "no-limitations", [])),
            ("no-metrics", PerformanceBody(
                ClientUserId, "VERIFIED", ValidMetric(), "no-metrics", includeMetric: false)),
            ("invalid-value", PerformanceBody(
                ClientUserId, "VERIFIED", InvalidPercentageMetric(), "invalid-value")),
            ("invalid-period", PerformanceBody(
                ClientUserId, "VERIFIED", InvalidPeriodMetric(), "invalid-period")),
            ("unsupported-metric", PerformanceBody(
                ClientUserId, "VERIFIED", UnsupportedMetric(), "unsupported-metric")),
            ("mismatched-unit", PerformanceBody(
                ClientUserId, "VERIFIED", MismatchedUnitMetric(), "mismatched-unit")),
        };
        foreach (var test in blockedBodies)
        {
            using var response = await RawCommandAsync(
                buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
                $"performance-{test.Key}", null, test.Body);
            await AssertProblemAsync(
                response, HttpStatusCode.Conflict, "PERFORMANCE_EVIDENCE_BLOCKED");
        }
        using var missingMethod = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-no-method", null,
            PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(),
                "no-method", methodology: " "));
        await AssertProblemAsync(
            missingMethod, HttpStatusCode.BadRequest, "VALIDATION_FAILED");
        using var invalidFile = await RawCommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-invalid-file", null,
            PerformanceBody(ClientUserId, "VERIFIED", ValidMetric(),
                "invalid-file", ["Panel data excludes devices without consent."],
                "%PDF-not-json"u8.ToArray()));
        await AssertProblemAsync(
            invalidFile, HttpStatusCode.BadRequest, "PERFORMANCE_EVIDENCE_FILE_REJECTED");
    }

    private static async Task AssertPerformanceVisibilityAndTaskAsync(
        HttpClient buyer,
        HttpClient client,
        HttpClient other,
        Guid campaignId,
        Guid evidenceId)
    {
        using var buyerView = await ReadAsync(
            buyer, BuyerTenantId, $"performance-evidence/{evidenceId}");
        var metric = Assert.Single(buyerView.RootElement.GetProperty("metrics").EnumerateArray());
        Assert.Equal("IMPRESSIONS", metric.GetProperty("metricType").GetString());
        Assert.Equal(125_000m, metric.GetProperty("value").GetDecimal());
        using var clientView = await ReadAsync(
            client, BuyerTenantId, $"performance-evidence/{evidenceId}");
        Assert.Equal("VERIFIED", clientView.RootElement.GetProperty("qualityStatus").GetString());
        using var unrelated = await other.GetAsync(
            $"/api/v1/tenants/{OtherTenantId}/performance-evidence/{evidenceId}");
        await AssertProblemAsync(unrelated, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
        using var tasks = await ReadAsync(client, BuyerTenantId, "human-tasks");
        var task = Assert.Single(tasks.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("taskType").GetString() == "PERFORMANCE_FACT_REVIEW" &&
                item.GetProperty("resourceId").GetGuid() == evidenceId);
        Assert.Equal(ClientUserId, task.GetProperty("assigneeUserId").GetGuid());
        using var campaign = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        Assert.Empty(campaign.RootElement.GetProperty("performanceEvidence").EnumerateArray());
        Assert.DoesNotContain("objectKey", campaign.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertPerformanceReviewGuardsAsync(
        HttpClient buyer,
        Guid campaignId,
        Guid evidenceId)
    {
        using var selfReview = await RawCommandAsync(
            buyer, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{evidenceId}:review",
            "performance-submitter-review", 1,
            new { approved = true, reason = "Submitter cannot review its own evidence." });
        await AssertProblemAsync(selfReview, HttpStatusCode.Forbidden, "TENANT_FORBIDDEN");
    }

    private static async Task<Guid> AssertUnusablePerformanceRejectedAsync(
        HttpClient buyer,
        HttpClient client,
        Guid campaignId)
    {
        using var submitted = await CommandAsync(
            buyer, BuyerTenantId, $"campaigns/{campaignId}/performance-evidence",
            "performance-unusable-submit", null,
            PerformanceBody(ClientUserId, "UNUSABLE", ValidMetric(), "unusable-source"));
        var id = submitted.RootElement.GetProperty("id").GetGuid();
        using var invalidApproval = await RawCommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{id}:review",
            "performance-unusable-approve", 1,
            new { approved = true, reason = "Unusable evidence cannot be approved." });
        await AssertProblemAsync(
            invalidApproval, HttpStatusCode.Conflict, "PERFORMANCE_EVIDENCE_BLOCKED");
        using var rejected = await CommandAsync(
            client, BuyerTenantId,
            $"campaigns/{campaignId}/performance-evidence/{id}:review",
            "performance-unusable-reject", 1,
            new { approved = false, reason = "The source is unusable for interpretation." });
        Assert.Equal("REJECTED", rejected.RootElement.GetProperty("status").GetString());
        return id;
    }

    private static async Task AssertReviewedPerformanceCampaignAsync(
        HttpClient buyer,
        Guid campaignId,
        Guid approvedEvidenceId,
        Guid rejectedEvidenceId)
    {
        using var campaign = await ReadAsync(buyer, BuyerTenantId, $"campaigns/{campaignId}");
        var evidence = campaign.RootElement.GetProperty("performanceEvidence").EnumerateArray()
            .ToArray();
        Assert.Equal(2, evidence.Length);
        Assert.Contains(evidence, item =>
            item.GetProperty("id").GetGuid() == approvedEvidenceId &&
            item.GetProperty("status").GetString() == "APPROVED");
        Assert.Contains(evidence, item =>
            item.GetProperty("id").GetGuid() == rejectedEvidenceId &&
            item.GetProperty("status").GetString() == "REJECTED");
        Assert.DoesNotContain("objectKey", campaign.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertPerformancePersistenceAsync(
        string connectionString,
        Guid campaignId,
        Guid evidenceId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.performance_evidence_sets"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.performance_metrics"));
        Assert.Equal(2, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.human_tasks " +
            "WHERE task_type_code = 'PERFORMANCE_FACT_REVIEW' AND status_code = 'COMPLETED'"));
        Assert.Equal(4, await CountAsync(connection,
            "SELECT count(*)::integer FROM commercial.audit_events " +
            "WHERE action_code LIKE 'performance_evidence.%'"));
        await AssertPerformanceImmutableAsync(connection, evidenceId);
        await using var retained = new NpgsqlCommand(
            "SELECT count(*)::integer FROM commercial.performance_evidence_sets " +
            "WHERE campaign_id = $1 AND status_code IN ('APPROVED', 'REJECTED')", connection);
        retained.Parameters.AddWithValue(campaignId);
        Assert.Equal(2, (int)(await retained.ExecuteScalarAsync())!);
    }

    private static async Task AssertPerformanceImmutableAsync(
        NpgsqlConnection connection,
        Guid evidenceId)
    {
        await using var source = new NpgsqlCommand(
            "UPDATE commercial.performance_evidence_sets " +
            "SET source_reference = 'changed' WHERE id = $1", connection);
        source.Parameters.AddWithValue(evidenceId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(source.ExecuteNonQueryAsync)).SqlState);
        await using var metric = new NpgsqlCommand(
            "UPDATE commercial.performance_metrics SET value = value + 1 " +
            "WHERE evidence_set_id = $1", connection);
        metric.Parameters.AddWithValue(evidenceId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(metric.ExecuteNonQueryAsync)).SqlState);
    }

    private static async Task AssertUnauthorizedPerformanceInsertAsync(
        string connectionString,
        Guid campaignId)
    {
        var id = Guid.Parse("98000000-0000-0000-0000-000000000001");
        const string hash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var role = new NpgsqlCommand(
            "SET LOCAL ROLE advertified_app", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var user = new NpgsqlCommand(
            "SELECT set_config('advertified.user_id', $1, true)", connection, transaction);
        user.Parameters.AddWithValue(ClientUserId.ToString());
        await user.ExecuteNonQueryAsync();
        await using var tenant = new NpgsqlCommand(
            "SELECT set_config('advertified.tenant_id', $1, true)", connection, transaction);
        tenant.Parameters.AddWithValue(BuyerTenantId.ToString());
        await tenant.ExecuteNonQueryAsync();
        await using var insert = new NpgsqlCommand("""
            INSERT INTO commercial.performance_evidence_sets (
                id, tenant_id, campaign_id, source_reference, file_name, media_type,
                size_bytes, content_sha256, signature_validated, malware_scan_status_code,
                protected_object_key, captured_at_utc, methodology, limitations_json,
                quality_status_code, status_code, reviewer_user_id, created_by,
                created_at_utc, version, updated_at_utc)
            VALUES ($1, $2, $3, 'direct-write', 'direct.json', 'application/json',
                2, $4, true, 'CLEAN', $5, '2026-10-01T08:00:00Z',
                'Untrusted direct write', '["unreviewed"]', 'VERIFIED', 'DRAFT',
                $6, $7, '2026-10-01T08:00:00Z', 0, '2026-10-01T08:00:00Z')
            """, connection, transaction);
        insert.Parameters.AddWithValue(id);
        insert.Parameters.AddWithValue(BuyerTenantId);
        insert.Parameters.AddWithValue(campaignId);
        insert.Parameters.AddWithValue(hash);
        insert.Parameters.AddWithValue(
            $"protected/{BuyerTenantId:N}/campaigns/{campaignId:N}/performance/{id:N}/{hash}");
        insert.Parameters.AddWithValue(ReviewerUserId);
        insert.Parameters.AddWithValue(ClientUserId);
        Assert.Equal(PostgresErrorCodes.RaiseException,
            (await Assert.ThrowsAsync<PostgresException>(insert.ExecuteNonQueryAsync)).SqlState);
        await transaction.RollbackAsync();
    }

    private static object PerformanceBody(
        Guid reviewerUserId,
        string qualityStatus,
        object metric,
        string sourceMarker,
        IReadOnlyList<string>? limitations = null,
        byte[]? content = null,
        bool includeMetric = true,
        string? methodology = null) => new
        {
            sourceReference = $"measurement-import:{sourceMarker}",
            capturedAtUtc = "2026-10-01T08:00:00Z",
            methodology = methodology ??
                "Aggregated verified supplier delivery logs for the booked flight.",
            limitations = limitations ?? ["Panel data excludes devices without consent."],
            qualityStatus,
            reviewerUserId,
            metrics = includeMetric ? new[] { metric } : Array.Empty<object>(),
            file = new
            {
                fileName = $"{sourceMarker}.json",
                mediaType = "application/json",
                content = content ?? Encoding.UTF8.GetBytes($"{{\"source\":\"{sourceMarker}\"}}"),
            },
        };

    private static object ValidMetric() => new
    {
        metricType = "IMPRESSIONS",
        value = 125_000m,
        unit = "COUNT",
        periodStart = "2026-09-01",
        periodEnd = "2026-09-30",
        sourceLocator = "verified-source.json#/facts/impressions",
    };

    private static object InvalidPercentageMetric() => new
    {
        metricType = "CLICK_THROUGH_RATE",
        value = 101m,
        unit = "PERCENT",
        periodStart = "2026-09-01",
        periodEnd = "2026-09-30",
        sourceLocator = "invalid-source.json#/facts/clickThroughRate",
    };

    private static object InvalidPeriodMetric() => new
    {
        metricType = "IMPRESSIONS",
        value = 125_000m,
        unit = "COUNT",
        periodStart = "2026-08-31",
        periodEnd = "2026-09-30",
        sourceLocator = "invalid-period.json#/facts/impressions",
    };

    private static object UnsupportedMetric() => new
    {
        metricType = "VIDEO_COMPLETIONS",
        value = 10m,
        unit = "COUNT",
        periodStart = "2026-09-01",
        periodEnd = "2026-09-30",
        sourceLocator = "unsupported.json#/facts/videoCompletions",
    };

    private static object MismatchedUnitMetric() => new
    {
        metricType = "IMPRESSIONS",
        value = 10m,
        unit = "PEOPLE",
        periodStart = "2026-09-01",
        periodEnd = "2026-09-30",
        sourceLocator = "wrong-unit.json#/facts/impressions",
    };
}
