using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed partial class CampaignRecordStore
{
    internal Task<CampaignSourceRow?> FindSourceFromConfirmedPaymentAsync(
        TenantId tenantId,
        Guid paymentIntentId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CampaignSourceRow>($"""
            SELECT brief.id AS "BriefId", proposal.brief_version_id AS "BriefVersionId",
                proposal.id AS "ProposalVersionId", option.id AS "ProposalOptionId",
                decision.id AS "ProposalDecisionId", payment.id AS "PaymentIntentId",
                proposal.title AS "Title", min(line.flight_start) AS "StartDate",
                max(line.flight_end) AS "EndDate", brief.owner_user_id AS "OwnerUserId",
                brief_version.measurement_json::text AS "MeasurementPlanJson",
                count(line.id)::integer AS "RequiredBookingCount"
            FROM commercial.payment_intents payment
            JOIN commercial.proposal_versions proposal
              ON proposal.tenant_id = payment.tenant_id
             AND proposal.id = payment.proposal_version_id
            JOIN commercial.proposal_decisions decision
              ON decision.tenant_id = proposal.tenant_id
             AND decision.proposal_version_id = proposal.id
            JOIN commercial.proposal_options option
              ON option.tenant_id = decision.tenant_id AND option.id = decision.option_id
             AND option.id = payment.proposal_option_id
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = option.tenant_id AND plan.id = option.plan_version_id
            JOIN commercial.media_plan_lines line
              ON line.tenant_id = option.tenant_id AND line.plan_version_id = option.plan_version_id
            JOIN commercial.campaign_briefs brief
              ON brief.tenant_id = proposal.tenant_id AND brief.id = proposal.brief_id
            JOIN commercial.brief_versions brief_version
              ON brief_version.tenant_id = proposal.tenant_id
             AND brief_version.id = proposal.brief_version_id
            WHERE payment.tenant_id = {tenantId.Value} AND payment.id = {paymentIntentId}
              AND payment.status_code = {MasterDataCodes.LifecycleStatuses.Confirmed}
              AND proposal.status_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND decision.decision_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND plan.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
            GROUP BY brief.id, proposal.brief_version_id, proposal.id, option.id,
                decision.id, payment.id, proposal.title, brief.owner_user_id,
                brief_version.measurement_json
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<CampaignRow?> FindAsync(
        Guid campaignId,
        bool forUpdate,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CampaignRow>(FormattableStringFactory.Create(
            CampaignSelect + " WHERE campaign.id = {1}" +
            (forUpdate ? " FOR UPDATE OF campaign" : string.Empty),
            MasterDataCodes.LifecycleStatuses.Confirmed, campaignId))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<List<CampaignRow>> ListAsync(CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CampaignRow>(FormattableStringFactory.Create(
            CampaignSelect + " ORDER BY campaign.updated_at_utc DESC, campaign.id DESC",
            MasterDataCodes.LifecycleStatuses.Confirmed))
            .ToListAsync(cancellationToken);
}
