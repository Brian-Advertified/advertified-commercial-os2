using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore
{
    internal Task<DeliveryProofSourceRow?> FindSourceAsync(
        Guid campaignId,
        Guid bookingId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<DeliveryProofSourceRow>($"""
            SELECT request.buyer_tenant_id AS "BuyerTenantId",
                request.supplier_tenant_id AS "SupplierTenantId",
                request.campaign_id AS "CampaignId", request.booking_id AS "BookingId",
                request.flight_start AS "FlightStart", request.flight_end AS "FlightEnd"
            FROM commercial.delivery_proof_requests request
            WHERE request.campaign_id = {campaignId}
              AND request.booking_id = {bookingId}
            """).SingleOrDefaultAsync(cancellationToken);
}
