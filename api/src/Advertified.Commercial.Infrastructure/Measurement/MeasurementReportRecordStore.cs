using System.Text.Json;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class MeasurementReportRecordStore(GovernanceDbContext dbContext)
{
    internal static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal const string ReportSelect = """
        SELECT report.id AS "Id", report.tenant_id AS "TenantId",
            report.campaign_id AS "CampaignId", report.version_no AS "VersionNumber",
            report.agent_run_id AS "AgentRunId",
            report.campaign_version AS "CampaignVersion",
            report.measurement_plan_json::text AS "MeasurementPlanJson",
            report.evidence_versions_json::text AS "EvidenceVersionsJson",
            report.interpretation_json::text AS "InterpretationJson",
            report.status_code AS "Status",
            report.approver_user_id AS "ApproverUserId",
            report.generated_by AS "GeneratedBy",
            report.generated_at_utc AS "GeneratedAtUtc",
            report.reviewed_by AS "ReviewedBy",
            report.reviewed_at_utc AS "ReviewedAtUtc",
            report.review_reason AS "ReviewReason", report.version AS "Version",
            report.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.measurement_report_versions report
        """;
}
