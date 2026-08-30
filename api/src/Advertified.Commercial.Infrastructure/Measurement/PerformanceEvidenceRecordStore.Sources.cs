using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class PerformanceEvidenceRecordStore
{
    internal Task<PerformanceEvidenceSourceRow?> FindSourceAsync(
        Guid campaignId,
        Guid reviewerUserId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PerformanceEvidenceSourceRow>($"""
            SELECT source.tenant_id AS "TenantId", source.campaign_id AS "CampaignId",
                source.campaign_start AS "CampaignStart",
                source.campaign_end AS "CampaignEnd",
                source.completed_at_utc AS "CompletedAtUtc",
                source.reviewer_user_id AS "ReviewerUserId"
            FROM commercial.performance_evidence_source({campaignId}, {reviewerUserId}) source
            """).SingleOrDefaultAsync(cancellationToken);
}
