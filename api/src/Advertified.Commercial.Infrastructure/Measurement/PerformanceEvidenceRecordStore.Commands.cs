using System.Text.Json;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class PerformanceEvidenceRecordStore
{
    internal async Task InsertAsync(
        Guid id,
        PerformanceEvidenceSourceRow source,
        PreparedPerformanceEvidence evidence,
        string objectKey,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await InsertDraftAsync(id, source, evidence, objectKey, envelope, now, cancellationToken);
        var metricsChanged = await InsertMetricsAsync(
            id, source, evidence, envelope, now, cancellationToken);
        if (metricsChanged != evidence.Metrics.Length)
        {
            throw new PerformanceEvidenceBlockedException();
        }
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.performance_evidence_sets
            SET status_code = {MasterDataCodes.LifecycleStatuses.Submitted},
                submitted_by = {envelope.ActorId.Value}, submitted_at_utc = {now},
                version = 1, updated_at_utc = {now}
            WHERE id = {id} AND tenant_id = {source.TenantId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft} AND version = 0
            """, cancellationToken);
        if (changed != 1) throw new PerformanceEvidenceBlockedException();
    }

    internal async Task ReviewAsync(
        PerformanceEvidenceRow evidence,
        CommandEnvelope<ReviewPerformanceEvidenceCommand> envelope,
        string decision,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.performance_evidence_sets
            SET status_code = {decision}, reviewed_by = {envelope.ActorId.Value},
                reviewed_at_utc = {now}, review_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {evidence.Id} AND tenant_id = {envelope.TenantId.Value}
              AND reviewer_user_id = {envelope.ActorId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Submitted}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    private async Task InsertDraftAsync(
        Guid id,
        PerformanceEvidenceSourceRow source,
        PreparedPerformanceEvidence evidence,
        string objectKey,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.performance_evidence_sets (
                    id, tenant_id, campaign_id, source_reference, file_name, media_type,
                    size_bytes, content_sha256, signature_validated,
                    malware_scan_status_code, protected_object_key, captured_at_utc,
                    methodology, limitations_json, quality_status_code, status_code,
                    reviewer_user_id, created_by, created_at_utc, version, updated_at_utc)
                VALUES ({id}, {source.TenantId}, {source.CampaignId},
                    {evidence.SourceReference}, {evidence.FileName}, {evidence.MediaType},
                    {evidence.Content.LongLength}, {evidence.Sha256}, true,
                    {MasterDataCodes.MalwareScanStatuses.Clean}, {objectKey},
                    {evidence.CapturedAtUtc}, {evidence.Methodology},
                    {JsonSerializer.Serialize(evidence.Limitations)}::jsonb,
                    {evidence.QualityStatus}, {MasterDataCodes.LifecycleStatuses.Draft},
                    {evidence.ReviewerUserId}, {envelope.ActorId.Value}, {now}, 0, {now})
                """, cancellationToken);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ux_performance_evidence_exact_content")
        {
            throw new PerformanceEvidenceBlockedException();
        }
    }

    private Task<int> InsertMetricsAsync(
        Guid evidenceId,
        PerformanceEvidenceSourceRow source,
        PreparedPerformanceEvidence evidence,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(evidence.Metrics.Select(item => new
        {
            id = Guid.NewGuid(),
            metricType = item.MetricType,
            item.Value,
            item.Unit,
            item.PeriodStart,
            item.PeriodEnd,
            item.SourceLocator,
        }));
        return DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.performance_metrics (
                id, tenant_id, campaign_id, evidence_set_id, metric_type_code,
                value, unit_code, period_start, period_end, source_locator,
                created_by, created_at_utc)
            SELECT item."id", {source.TenantId}, {source.CampaignId}, {evidenceId},
                item."metricType", item."Value", item."Unit", item."PeriodStart",
                item."PeriodEnd", item."SourceLocator", {envelope.ActorId.Value}, {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS item(
                "id" uuid, "metricType" varchar(100), "Value" numeric(20,6),
                "Unit" varchar(100), "PeriodStart" date, "PeriodEnd" date,
                "SourceLocator" varchar(500))
            """, cancellationToken);
    }
}
