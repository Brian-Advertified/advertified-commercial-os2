using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingRecordStore
{
    internal Task<int> InsertAsync(
        Guid bookingId,
        CommandEnvelope<Advertified.Commercial.Application.Booking.CreateBookingCommand> envelope,
        BookingSourceRow source,
        CommercialPolicyRow policy,
        CommercialMoneyBreakdown money,
        string terms,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.bookings (
                id, buyer_tenant_id, supplier_tenant_id,
                proposal_version_id, proposal_option_id, proposal_decision_id,
                plan_version_id, media_plan_line_id, marketplace_listing_version_id,
                commercial_policy_version_id, supplier_id, inventory_product_id,
                product_version_id, rate_id, availability_id,
                supplier_name, product_name, channel_code, geography,
                flight_start, flight_end, running_periods, quantity,
                supplier_cost_minor, markup_minor, commission_minor,
                management_fee_minor, client_price_minor, fees_minor, vat_minor,
                booking_approval_threshold_minor, currency_code, terms,
                status_code, created_by, created_at_utc, version, updated_at_utc)
            VALUES (
                {bookingId}, {envelope.TenantId.Value}, {source.SupplierTenantId},
                {source.ProposalVersionId}, {source.ProposalOptionId},
                {source.ProposalDecisionId}, {source.PlanVersionId},
                {source.MediaPlanLineId}, {source.MarketplaceListingVersionId},
                {policy.Id}, {source.SupplierId}, {source.InventoryProductId},
                {source.ProductVersionId}, {source.RateId}, {source.AvailabilityId},
                {source.SupplierName}, {source.ProductName}, {source.Channel},
                {source.Geography}, {source.FlightStart}, {source.FlightEnd},
                {source.RunningPeriods}, {source.Quantity}, {money.SupplierCostMinor},
                {money.MarkupMinor}, {money.CommissionMinor}, {money.ManagementFeeMinor},
                {money.TotalMinor}, {money.MarkupMinor + money.CommissionMinor + money.ManagementFeeMinor},
                {money.VatMinor}, {policy.BookingApprovalThresholdMinor}, {policy.Currency},
                {terms}, {MasterDataCodes.LifecycleStatuses.Draft},
                {envelope.ActorId.Value}, {now}, 1, {now})
            ON CONFLICT (buyer_tenant_id, proposal_decision_id, media_plan_line_id)
                DO NOTHING
            """, cancellationToken);

    internal async Task RequestConfirmationAsync(
        BookingRow booking,
        CommandEnvelope<Advertified.Commercial.Application.Booking.RequestBookingConfirmationCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.bookings
            SET status_code = {MasterDataCodes.LifecycleStatuses.PendingSupplier},
                requested_by = {envelope.ActorId.Value}, requested_at_utc = {now},
                request_reason = {reason}, version = version + 1, updated_at_utc = {now}
            WHERE id = {booking.Id} AND buyer_tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Draft}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    internal async Task ConfirmAsync(
        BookingRow booking,
        CommandEnvelope<Advertified.Commercial.Application.Booking.ConfirmBookingCommand> envelope,
        string reason,
        string? note,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.bookings
            SET status_code = {MasterDataCodes.LifecycleStatuses.Confirmed},
                confirmed_by = {envelope.ActorId.Value}, confirmed_at_utc = {now},
                confirmation_reason = {reason}, supplier_note = {note},
                terms_accepted = true, version = version + 1, updated_at_utc = {now}
            WHERE id = {booking.Id} AND supplier_tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.PendingSupplier}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
