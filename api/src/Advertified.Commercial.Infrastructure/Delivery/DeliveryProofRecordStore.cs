using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore(GovernanceDbContext dbContext)
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

    internal const string ProofSelect = """
        SELECT proof.id AS "Id", proof.buyer_tenant_id AS "BuyerTenantId",
            proof.supplier_tenant_id AS "SupplierTenantId",
            proof.campaign_id AS "CampaignId", proof.booking_id AS "BookingId",
            proof.proof_type_code AS "ProofType", proof.file_name AS "FileName",
            proof.media_type AS "MediaType", proof.size_bytes AS "SizeBytes",
            proof.content_sha256 AS "ContentSha256",
            proof.signature_validated AS "SignatureValidated",
            proof.malware_scan_status_code AS "MalwareScanStatus",
            proof.captured_at_utc AS "CapturedAtUtc",
            proof.location_description AS "LocationDescription",
            proof.latitude AS "Latitude", proof.longitude AS "Longitude",
            proof.source_reference AS "SourceReference",
            proof.submission_reason AS "SubmissionReason",
            proof.status_code AS "Status", proof.submitted_by AS "SubmittedBy",
            proof.submitter_tenant_id AS "SubmitterTenantId",
            proof.submitted_at_utc AS "SubmittedAtUtc",
            proof.reviewed_by AS "ReviewedBy", proof.reviewed_at_utc AS "ReviewedAtUtc",
            proof.review_reason AS "ReviewReason", proof.version AS "Version",
            proof.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.delivery_proofs proof
        """;
}
