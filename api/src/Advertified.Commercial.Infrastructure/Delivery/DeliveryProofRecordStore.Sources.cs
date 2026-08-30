using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed partial class DeliveryProofRecordStore
{
    internal Task<DeliveryProofSourceRow?> FindSourceAsync(
        Guid campaignId,
        Guid bookingId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<DeliveryProofSourceRow>($"""
            SELECT source.buyer_tenant_id AS "BuyerTenantId",
                source.supplier_tenant_id AS "SupplierTenantId",
                source.campaign_id AS "CampaignId", source.booking_id AS "BookingId",
                source.campaign_owner_user_id AS "CampaignOwnerUserId",
                source.campaign_version AS "CampaignVersion",
                source.flight_start AS "FlightStart", source.flight_end AS "FlightEnd"
            FROM commercial.delivery_proof_source({campaignId}, {bookingId}) source
            """).SingleOrDefaultAsync(cancellationToken);
}
