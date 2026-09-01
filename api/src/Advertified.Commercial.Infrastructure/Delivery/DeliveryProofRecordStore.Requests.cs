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
                request.latest_proof_id AS "LatestProofId",
                request.latest_proof_status AS "LatestProofStatus"
            FROM commercial.supplier_delivery_proof_requests() request
            """).ToListAsync(cancellationToken);
}
