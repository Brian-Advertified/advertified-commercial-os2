using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore
{
    internal Task<List<DeliveryProofRequestRow>> ListRequestsAsync(
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<DeliveryProofRequestRow>($"""
            SELECT request.campaign_id AS "CampaignId",
                request.booking_id AS "BookingId",
                request.supplier_name AS "SupplierName",
                request.product_name AS "ProductName",
                request.channel_code AS "Channel",
                request.geography AS "Geography",
                request.flight_start AS "FlightStart",
                request.flight_end AS "FlightEnd",
                request.proof_requested_at_utc AS "ProofRequestedAtUtc",
                request.proof_request_reason AS "ProofRequestReason",
                proof.id AS "LatestProofId",
                proof.status_code AS "LatestProofStatus"
            FROM commercial.delivery_proof_requests request
            LEFT JOIN LATERAL (
                SELECT candidate.id, candidate.status_code,
                    candidate.submission_sequence
                FROM commercial.delivery_proofs candidate
                WHERE candidate.buyer_tenant_id = request.buyer_tenant_id
                  AND candidate.supplier_tenant_id = request.supplier_tenant_id
                  AND candidate.campaign_id = request.campaign_id
                  AND candidate.booking_id = request.booking_id
                ORDER BY candidate.submission_sequence DESC
                LIMIT 1) proof ON true
            WHERE request.supplier_tenant_id = commercial.current_tenant_id()
            ORDER BY request.proof_requested_at_utc DESC, request.booking_id
            LIMIT 200
            """).ToListAsync(cancellationToken);
}
