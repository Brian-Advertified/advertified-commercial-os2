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
        return (await BuildViewsAsync([row], evidenceStore, [], cancellationToken))[0];
    }

    internal async Task<IReadOnlyList<MeasurementReportView>> ListApprovedCampaignAsync(
        Guid campaignId,
        PerformanceEvidenceRecordStore evidenceStore,
        CancellationToken cancellationToken,
        IReadOnlyList<PerformanceEvidenceView>? loadedEvidence = null)
    {
        var rows = await DbContext.Database.SqlQuery<MeasurementReportRow>(
            FormattableStringFactory.Create(
                ReportSelect + " WHERE report.campaign_id = {0}" +
                " AND report.status_code = {1} ORDER BY report.version_no, report.id",
                campaignId, MasterDataCodes.LifecycleStatuses.Approved))
            .ToListAsync(cancellationToken);
        return await BuildViewsAsync(rows, evidenceStore, loadedEvidence ?? [], cancellationToken);
    }

    private static async Task<IReadOnlyList<MeasurementReportView>> BuildViewsAsync(
        IReadOnlyList<MeasurementReportRow> rows,
        PerformanceEvidenceRecordStore evidenceStore,
        IReadOnlyList<PerformanceEvidenceView> loadedEvidence,
        CancellationToken cancellationToken)
    {
        var byId = loadedEvidence.ToDictionary(view => view.Id);
        var missingIds = rows.SelectMany(row => row.EvidenceVersions())
            .Select(source => source.Id).Distinct().Where(id => !byId.ContainsKey(id));
        foreach (var view in await evidenceStore.GetViewsAsync(missingIds, cancellationToken))
            byId.Add(view.Id, view);
        return rows.Select(row => ToView(row, row.EvidenceVersions().Select(source =>
        {
            if (!byId.TryGetValue(source.Id, out var view) || view.Version != source.Version ||
                view.CampaignId != row.CampaignId)
                throw new InvalidOperationException("Measurement evidence is unavailable.");
            return view;
        }).ToArray())).ToArray();
    }

    private static MeasurementReportView ToView(
        MeasurementReportRow row,
        IReadOnlyList<PerformanceEvidenceView> evidence) => new(
            row.Id, row.CampaignId, row.VersionNumber, row.CampaignVersion,
            row.MeasurementPlan(), evidence, row.Interpretation(), row.Status,
            row.ApproverUserId, row.GeneratedBy, row.GeneratedAtUtc, row.ReviewedBy,
            row.ReviewedAtUtc, row.ReviewReason, row.Version, row.UpdatedAtUtc);
}
