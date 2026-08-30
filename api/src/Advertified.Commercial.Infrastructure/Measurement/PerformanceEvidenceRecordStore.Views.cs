using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class PerformanceEvidenceRecordStore
{
    internal Task<PerformanceEvidenceRow?> FindAsync(
        Guid evidenceId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = EvidenceSelect + " WHERE evidence.id = {0}" +
            (forUpdate ? " FOR UPDATE OF evidence" : string.Empty);
        return DbContext.Database.SqlQuery<PerformanceEvidenceRow>(
            FormattableStringFactory.Create(sql, evidenceId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<PerformanceMetricRow>> ListMetricsAsync(
        Guid evidenceId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PerformanceMetricRow>($"""
            SELECT metric.id AS "Id", metric.evidence_set_id AS "EvidenceId",
                metric.metric_type_code AS "MetricType", metric.value AS "Value",
                metric.unit_code AS "Unit", metric.period_start AS "PeriodStart",
                metric.period_end AS "PeriodEnd",
                metric.source_locator AS "SourceLocator"
            FROM commercial.performance_metrics metric
            WHERE metric.evidence_set_id = {evidenceId}
            ORDER BY metric.metric_type_code, metric.period_start, metric.id
            """).ToListAsync(cancellationToken);

    internal async Task<PerformanceEvidenceView?> GetViewAsync(
        Guid evidenceId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(evidenceId, forUpdate, cancellationToken);
        if (row is null) return null;
        return PerformanceEvidenceRowMapper.ToView(
            row, await ListMetricsAsync(evidenceId, cancellationToken));
    }

    internal async Task<IReadOnlyList<PerformanceEvidenceView>> ListCampaignViewsAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var rows = await DbContext.Database.SqlQuery<PerformanceEvidenceRow>(
            FormattableStringFactory.Create(
                EvidenceSelect +
                " WHERE evidence.campaign_id = {0}" +
                " AND evidence.status_code IN ({1}, {2})" +
                " ORDER BY evidence.submitted_at_utc, evidence.id",
                campaignId,
                MasterDataCodes.LifecycleStatuses.Approved,
                MasterDataCodes.LifecycleStatuses.Rejected)).ToListAsync(cancellationToken);
        var views = new List<PerformanceEvidenceView>(rows.Count);
        foreach (var row in rows)
        {
            views.Add(PerformanceEvidenceRowMapper.ToView(
                row, await ListMetricsAsync(row.Id, cancellationToken)));
        }
        return views;
    }
}
