using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore
{
    internal async Task StartAsync(
        CampaignRow row,
        CommandEnvelope<StartCampaignCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Live},
                started_by = {envelope.ActorId.Value}, started_at_utc = {now},
                start_reason = {reason}, version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Ready}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    internal async Task CompleteAsync(
        CampaignRow row,
        CommandEnvelope<CompleteCampaignCommand> envelope,
        string completionReason,
        string proofRequestReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed},
                completed_by = {envelope.ActorId.Value}, completed_at_utc = {now},
                completion_reason = {completionReason},
                proof_requested_by = {envelope.ActorId.Value}, proof_requested_at_utc = {now},
                proof_request_reason = {proofRequestReason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Live}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();

        var requests = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.delivery_proof_requests (
                buyer_tenant_id, supplier_tenant_id, campaign_id, booking_id,
                supplier_name, product_name, channel_code, geography,
                flight_start, flight_end, campaign_owner_user_id, opportunity_id,
                proof_requested_by, proof_requested_at_utc, proof_request_reason)
            SELECT booking.buyer_tenant_id, booking.supplier_tenant_id,
                {row.Id}, booking.id, booking.supplier_name, booking.product_name,
                booking.channel_code, booking.geography, booking.flight_start,
                booking.flight_end, {row.OwnerUserId}, brief.opportunity_id,
                {envelope.ActorId.Value}, {now}, {proofRequestReason}
            FROM commercial.bookings booking
            JOIN commercial.campaign_briefs brief
              ON brief.tenant_id = {envelope.TenantId.Value}
             AND brief.id = {row.BriefId}
            WHERE booking.buyer_tenant_id = {envelope.TenantId.Value}
              AND booking.proposal_decision_id = {row.ProposalDecisionId}
              AND booking.plan_version_id = {row.PlanVersionId}
              AND booking.status_code = {MasterDataCodes.LifecycleStatuses.Confirmed}
            """, cancellationToken);
        if (requests != row.RequiredBookingCount)
            throw new CampaignDeliveryBlockedException();
    }
}
