using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class MeasurementReportRecordStore
{
    internal Task<List<MeasurementReportSummary>> ListSummariesAsync(
        TenantId tenantId, int take, Guid? cursor, CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<MeasurementReportSummary>($"""
            SELECT report.id AS "Id", report.campaign_id AS "CampaignId",
                campaign.title AS "CampaignTitle", report.version_no AS "VersionNumber",
                report.status_code AS "Status",
                jsonb_array_length(report.evidence_versions_json) AS "EvidenceCount",
                report.updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.measurement_report_versions report
            JOIN commercial.campaigns campaign
              ON campaign.tenant_id = report.tenant_id AND campaign.id = report.campaign_id
            WHERE report.tenant_id = {tenantId.Value}
              AND ({cursor}::uuid IS NULL OR report.id > {cursor})
            ORDER BY report.id LIMIT {take}
            """).ToListAsync(cancellationToken);

    internal Task<List<MeasurementCampaignSummary>> ListCampaignSummariesAsync(
        TenantId tenantId, int take, Guid? cursor, CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<MeasurementCampaignSummary>($"""
            SELECT campaign.id AS "Id", campaign.title AS "Title",
                campaign.status_code AS "Status", campaign.updated_at_utc AS "UpdatedAtUtc",
                (SELECT count(*)::integer FROM commercial.performance_evidence_sets evidence
                 WHERE evidence.tenant_id = campaign.tenant_id
                   AND evidence.campaign_id = campaign.id
                   AND evidence.status_code IN (
                       {MasterDataCodes.LifecycleStatuses.Approved},
                       {MasterDataCodes.LifecycleStatuses.Rejected})) AS "EvidenceCount",
                (SELECT count(*)::integer FROM commercial.measurement_report_versions report
                 WHERE report.tenant_id = campaign.tenant_id
                   AND report.campaign_id = campaign.id) AS "ReportCount"
            FROM commercial.campaigns campaign
            WHERE campaign.tenant_id = {tenantId.Value}
              AND ({cursor}::uuid IS NULL OR campaign.id > {cursor})
            ORDER BY campaign.id LIMIT {take}
            """).ToListAsync(cancellationToken);
}
