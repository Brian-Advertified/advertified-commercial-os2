using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed partial class PerformanceEvidenceRecordStore(GovernanceDbContext dbContext)
{
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

    internal const string EvidenceSelect = """
        SELECT evidence.id AS "Id", evidence.tenant_id AS "TenantId",
            evidence.campaign_id AS "CampaignId",
            evidence.source_reference AS "SourceReference",
            evidence.file_name AS "FileName", evidence.media_type AS "MediaType",
            evidence.size_bytes AS "SizeBytes", evidence.content_sha256 AS "ContentSha256",
            evidence.signature_validated AS "SignatureValidated",
            evidence.malware_scan_status_code AS "MalwareScanStatus",
            evidence.captured_at_utc AS "CapturedAtUtc",
            evidence.methodology AS "Methodology",
            evidence.limitations_json::text AS "LimitationsJson",
            evidence.quality_status_code AS "QualityStatus",
            evidence.status_code AS "Status",
            evidence.reviewer_user_id AS "ReviewerUserId",
            evidence.submitted_by AS "SubmittedBy",
            evidence.submitted_at_utc AS "SubmittedAtUtc",
            evidence.reviewed_by AS "ReviewedBy",
            evidence.reviewed_at_utc AS "ReviewedAtUtc",
            evidence.review_reason AS "ReviewReason",
            evidence.version AS "Version", evidence.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.performance_evidence_sets evidence
        """;
}
