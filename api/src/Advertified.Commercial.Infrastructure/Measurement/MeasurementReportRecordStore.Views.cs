using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class MeasurementReportRecordStore
{
    internal Task<MeasurementReportRow?> FindAsync(
        Guid reportId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var sql = ReportSelect + " WHERE report.id = {0}" +
            (forUpdate ? " FOR UPDATE OF report" : string.Empty);
        return DbContext.Database.SqlQuery<MeasurementReportRow>(
            FormattableStringFactory.Create(sql, reportId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal async Task<MeasurementReportView?> GetViewAsync(
        Guid reportId,
        PerformanceEvidenceRecordStore evidenceStore,
        CancellationToken cancellationToken)
    {
        var row = await FindAsync(reportId, false, cancellationToken);
        if (row is null) return null;
        var evidence = new List<PerformanceEvidenceView>();
        foreach (var source in row.EvidenceVersions())
        {
            var view = await evidenceStore.GetViewAsync(source.Id, false, cancellationToken);
            if (view is null || view.Version != source.Version)
                throw new InvalidOperationException("Measurement evidence is unavailable.");
            evidence.Add(view);
        }
        return ToView(row, evidence);
    }

    internal async Task<IReadOnlyList<MeasurementReportView>> ListApprovedCampaignAsync(
        Guid campaignId,
        PerformanceEvidenceRecordStore evidenceStore,
        CancellationToken cancellationToken)
    {
        var rows = await DbContext.Database.SqlQuery<MeasurementReportRow>(
            FormattableStringFactory.Create(
                ReportSelect + " WHERE report.campaign_id = {0}" +
                " AND report.status_code = {1} ORDER BY report.version_no, report.id",
                campaignId, MasterDataCodes.LifecycleStatuses.Approved))
            .ToListAsync(cancellationToken);
        var reports = new List<MeasurementReportView>(rows.Count);
        foreach (var row in rows)
        {
            var view = await GetViewAsync(row.Id, evidenceStore, cancellationToken)
                ?? throw new InvalidOperationException("Measurement report is unavailable.");
            reports.Add(view);
        }
        return reports;
    }

    private static MeasurementReportView ToView(
        MeasurementReportRow row,
        IReadOnlyList<PerformanceEvidenceView> evidence) => new(
            row.Id, row.CampaignId, row.VersionNumber, row.CampaignVersion,
            row.MeasurementPlan(), evidence, row.Interpretation(), row.Status,
            row.ApproverUserId, row.GeneratedBy, row.GeneratedAtUtc, row.ReviewedBy,
            row.ReviewedAtUtc, row.ReviewReason, row.Version, row.UpdatedAtUtc);
}
