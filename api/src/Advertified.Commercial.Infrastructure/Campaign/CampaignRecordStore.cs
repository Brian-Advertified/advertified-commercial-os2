using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore(GovernanceDbContext dbContext)
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

    internal const string CampaignSelect = """
        SELECT campaign.id AS "Id", campaign.brief_id AS "BriefId",
            campaign.brief_version_id AS "BriefVersionId",
            campaign.proposal_version_id AS "ProposalVersionId",
            campaign.proposal_option_id AS "ProposalOptionId",
            campaign.proposal_decision_id AS "ProposalDecisionId",
            campaign.plan_version_id AS "PlanVersionId",
            campaign.payment_intent_id AS "PaymentIntentId",
            payment.status_code AS "FundingStatus",
            campaign.title AS "Title", campaign.start_date AS "StartDate",
            campaign.end_date AS "EndDate", campaign.owner_user_id AS "OwnerUserId",
            campaign.measurement_plan_json::text AS "MeasurementPlanJson",
            campaign.status_code AS "Status",
            (SELECT count(*)::integer FROM commercial.media_plan_lines line
             WHERE line.tenant_id = campaign.tenant_id
               AND line.plan_version_id = campaign.plan_version_id) AS "RequiredBookingCount",
            (SELECT count(*)::integer FROM commercial.bookings booking
             WHERE booking.buyer_tenant_id = campaign.tenant_id
               AND booking.proposal_decision_id = campaign.proposal_decision_id
               AND booking.status_code = {0}) AS "ConfirmedBookingCount",
            campaign.created_by AS "CreatedBy", campaign.created_at_utc AS "CreatedAtUtc",
            campaign.bookings_confirmed_by AS "BookingsConfirmedBy",
            campaign.bookings_confirmed_at_utc AS "BookingsConfirmedAtUtc",
            campaign.booking_confirmation_reason AS "BookingConfirmationReason",
            campaign.creative_requested_by AS "CreativeRequestedBy",
            campaign.creative_requested_at_utc AS "CreativeRequestedAtUtc",
            campaign.creative_request_reason AS "CreativeRequestReason",
            campaign.creative_approved_by AS "CreativeApprovedBy",
            campaign.creative_approved_at_utc AS "CreativeApprovedAtUtc",
            campaign.creative_approval_reason AS "CreativeApprovalReason",
            campaign.started_by AS "StartedBy",
            campaign.started_at_utc AS "StartedAtUtc",
            campaign.start_reason AS "StartReason",
            campaign.completed_by AS "CompletedBy",
            campaign.completed_at_utc AS "CompletedAtUtc",
            campaign.completion_reason AS "CompletionReason",
            campaign.proof_requested_by AS "ProofRequestedBy",
            campaign.proof_requested_at_utc AS "ProofRequestedAtUtc",
            campaign.proof_request_reason AS "ProofRequestReason",
            campaign.version AS "Version", campaign.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.campaigns campaign
        JOIN commercial.payment_intents payment
          ON payment.tenant_id = campaign.tenant_id
         AND payment.id = campaign.payment_intent_id
        """;
}
