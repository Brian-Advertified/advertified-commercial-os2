using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore
{
    internal async Task<CampaignRow> CreatePlannedAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        Guid paymentIntentId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var source = await FindSourceFromConfirmedPaymentAsync(
            envelope.TenantId, paymentIntentId, cancellationToken)
            ?? throw new CampaignReadinessBlockedException();
        if (source.RequiredBookingCount <= 0)
            throw new CampaignReadinessBlockedException();
        var id = Guid.NewGuid();
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.campaigns (
                id, tenant_id, brief_id, brief_version_id, proposal_version_id,
                proposal_option_id, proposal_decision_id, plan_version_id,
                payment_intent_id, title, start_date, end_date, owner_user_id,
                measurement_plan_json, status_code, created_by, created_at_utc,
                version, updated_at_utc)
            SELECT {id}, {envelope.TenantId.Value}, {source.BriefId},
                {source.BriefVersionId}, {source.ProposalVersionId},
                {source.ProposalOptionId}, {source.ProposalDecisionId}, option.plan_version_id,
                {source.PaymentIntentId}, {source.Title}, {source.StartDate}, {source.EndDate},
                {source.OwnerUserId}, CAST({source.MeasurementPlanJson} AS jsonb),
                {MasterDataCodes.LifecycleStatuses.Planned}, {envelope.ActorId.Value},
                {now}, 1, {now}
            FROM commercial.proposal_options option
            WHERE option.tenant_id = {envelope.TenantId.Value}
              AND option.id = {source.ProposalOptionId}
            ON CONFLICT (tenant_id, proposal_decision_id) DO NOTHING
            """, cancellationToken);
        if (changed != 1) throw new InvalidOperationException("The campaign already exists.");
        return await FindAsync(id, false, cancellationToken)
            ?? throw new InvalidOperationException("The campaign was not persisted.");
    }

    internal async Task ConfirmBookingsAsync(
        CampaignRow row,
        CommandEnvelope<ConfirmCampaignBookingsCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (row.RequiredBookingCount <= 0 ||
            row.ConfirmedBookingCount != row.RequiredBookingCount)
            throw new CampaignReadinessBlockedException();
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.campaigns
            SET status_code = {MasterDataCodes.LifecycleStatuses.Booked},
                bookings_confirmed_by = {envelope.ActorId.Value},
                bookings_confirmed_at_utc = {now},
                booking_confirmation_reason = {reason},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Planned}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
