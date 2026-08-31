using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class MeasurementReportRecordStore
{
    internal Task<MeasurementReportSourceRow?> FindSourceAsync(
        Guid campaignId,
        Guid approverUserId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<MeasurementReportSourceRow>($"""
            SELECT source.tenant_id AS "TenantId", source.campaign_id AS "CampaignId",
                source.opportunity_id AS "OpportunityId",
                source.campaign_version AS "CampaignVersion",
                source.measurement_plan_json::text AS "MeasurementPlanJson",
                source.approver_user_id AS "ApproverUserId"
            FROM commercial.measurement_report_source({campaignId}, {approverUserId}) source
            """).SingleOrDefaultAsync(cancellationToken);
}
