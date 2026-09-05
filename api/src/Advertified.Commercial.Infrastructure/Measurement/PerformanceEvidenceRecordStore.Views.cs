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
        ListMetricsAsync([evidenceId], cancellationToken);

    private Task<List<PerformanceMetricRow>> ListMetricsAsync(
        Guid[] evidenceIds,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PerformanceMetricRow>($"""
            SELECT metric.id AS "Id", metric.evidence_set_id AS "EvidenceId",
                metric.metric_type_code AS "MetricType", metric.value AS "Value",
                metric.unit_code AS "Unit", metric.period_start AS "PeriodStart",
                metric.period_end AS "PeriodEnd",
                metric.source_locator AS "SourceLocator"
            FROM commercial.performance_metrics metric
            WHERE metric.evidence_set_id = ANY({evidenceIds})
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
        return await BuildViewsAsync(rows, cancellationToken);
    }

    internal async Task<IReadOnlyList<PerformanceEvidenceView>> GetViewsAsync(
        IEnumerable<Guid> evidenceIds,
        CancellationToken cancellationToken)
    {
        var views = new List<PerformanceEvidenceView>();
        foreach (var batch in evidenceIds.Distinct().Chunk(256))
        {
            var rows = await DbContext.Database.SqlQuery<PerformanceEvidenceRow>(
                FormattableStringFactory.Create(
                    EvidenceSelect + " WHERE evidence.id = ANY({0})", (object)batch))
                .ToListAsync(cancellationToken);
            views.AddRange(await BuildViewsAsync(rows, cancellationToken));
        }
        return views;
    }

    private async Task<IReadOnlyList<PerformanceEvidenceView>> BuildViewsAsync(
        IReadOnlyList<PerformanceEvidenceRow> rows,
        CancellationToken cancellationToken)
    {
        var metrics = new List<PerformanceMetricRow>();
        foreach (var batch in rows.Select(row => row.Id).Distinct().Chunk(256))
            metrics.AddRange(await ListMetricsAsync(batch, cancellationToken));
        var byEvidence = metrics.ToLookup(metric => metric.EvidenceId);
        return rows.Select(row => PerformanceEvidenceRowMapper.ToView(
            row, byEvidence[row.Id].ToArray())).ToArray();
    }
}
