using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore
{
    internal async Task<Guid> InsertAsync(
        Guid id,
        DeliveryProofSourceRow source,
        PreparedDeliveryProof proof,
        string objectKey,
        CommandEnvelope<SubmitDeliveryProofCommand> envelope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int changed;
        try
        {
            changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.delivery_proofs (
                    id, buyer_tenant_id, supplier_tenant_id, campaign_id, booking_id,
                    proof_type_code, file_name, media_type, size_bytes, content_sha256,
                    signature_validated, malware_scan_status_code, protected_object_key,
                    captured_at_utc, location_description, latitude, longitude,
                    source_reference, submission_reason, status_code,
                    submitted_by, submitter_tenant_id, submitted_at_utc, version, updated_at_utc)
                VALUES ({id}, {source.BuyerTenantId}, {source.SupplierTenantId},
                    {source.CampaignId}, {source.BookingId}, {proof.ProofType}, {proof.FileName},
                    {proof.MediaType}, {proof.Content.LongLength}, {proof.Sha256}, true,
                    {MasterDataCodes.MalwareScanStatuses.Clean}, {objectKey},
                    {proof.CapturedAtUtc}, {proof.LocationDescription}, {proof.Latitude},
                    {proof.Longitude}, {proof.SourceReference}, {proof.Reason},
                    {MasterDataCodes.LifecycleStatuses.Submitted}, {envelope.ActorId.Value},
                    {envelope.TenantId.Value}, {now}, 1, {now})
                """, cancellationToken);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == "ux_delivery_proof_exact_content")
        {
            throw new DeliveryProofBlockedException();
        }
        if (changed != 1) throw new DeliveryProofBlockedException();
        return id;
    }

    internal async Task ReviewAsync(
        DeliveryProofRow proof,
        CommandEnvelope<ReviewDeliveryProofCommand> envelope,
        string decision,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.delivery_proofs
            SET status_code = {decision}, reviewed_by = {envelope.ActorId.Value},
                reviewed_at_utc = {now}, review_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {proof.Id} AND buyer_tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Submitted}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
