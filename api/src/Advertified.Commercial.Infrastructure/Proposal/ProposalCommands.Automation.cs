using System.Net.Mail;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Proposal;

public sealed partial class ProposalCommands
{
    private async Task<CommandOutcome> RecordAutomatedDeliveryOutcomeAsync(
        Guid proposalVersionId,
        CommandEnvelope<RecordAutomatedProposalDeliveryCommand> envelope,
        CancellationToken cancellationToken)
    {
        var proposal = await LoadOwnedProposalAsync(
            proposalVersionId, envelope, cancellationToken);
        var recipient = NormalizeEmail(envelope.Command.RecipientEmail);
        var providerMessageId = Required(
            envelope.Command.ProviderMessageId,
            300,
            nameof(envelope.Command.ProviderMessageId));
        var now = timeProvider.GetUtcNow();
        if (proposal.Status != MasterDataCodes.LifecycleStatuses.Approved ||
            await store.FindDocumentAsync(
                envelope.TenantId, proposalVersionId, cancellationToken) is null)
        {
            throw new ProposalDocumentRequiredException();
        }
        var authorised = await store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1
                FROM commercial.email_proposal_automation_runs run
                JOIN commercial.inbound_campaign_emails email
                  ON email.tenant_id = run.tenant_id
                 AND email.id = run.inbound_email_id
                JOIN commercial.inbound_mailboxes mailbox
                  ON mailbox.tenant_id = email.tenant_id
                 AND mailbox.id = email.mailbox_id
                WHERE run.tenant_id = {envelope.TenantId.Value}
                  AND run.id = {envelope.Command.AutomationRunId}
                  AND run.proposal_version_id = {proposalVersionId}
                  AND run.document_id IS NOT NULL
                  AND run.campaign_mode_code = {MasterDataCodes.CampaignModes.OohOnly}
                  AND run.status_code = {MasterDataCodes.EmailAutomationStatuses.Processing}
                  AND run.checkpoint_code = {MasterDataCodes.EmailAutomationCheckpoints.DeliveryAccepted}
                  AND run.delivery_requested_at_utc IS NOT NULL
                  AND run.delivery_accepted_at_utc IS NOT NULL
                  AND run.delivery_provider_code IS NOT NULL
                  AND run.delivery_provider_id = {providerMessageId}
                  AND mailbox.owner_user_id = {envelope.ActorId.Value}
                  AND email.reply_to_email = {recipient}
                  AND run.delivery_idempotency_key IS NOT NULL) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!authorised)
        {
            throw new UnauthorizedAccessException(
                "Automated proposal delivery is not authorised for this exact run.");
        }
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.proposal_versions
            SET status_code = {MasterDataCodes.LifecycleStatuses.Sent},
                shared_at_utc = {now}, version = version + 1
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {proposalVersionId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND inventory_review_status_code =
                    {MasterDataCodes.ProposalInventoryReviewStatuses.Current}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        var updated = proposal with
        {
            Status = MasterDataCodes.LifecycleStatuses.Sent,
            Version = proposal.Version + 1,
        };
        var view = await store.BuildViewAsync(
            envelope.TenantId, updated, cancellationToken);
        return ProposalOutcome(
            envelope,
            view,
            proposalVersionId,
            updated.Version,
            MasterDataReferences.CommercialActions.ProposalShared,
            MasterDataReferences.CommercialEventTypes.ProposalShared,
            now);
    }

    private static string NormalizeEmail(string value)
    {
        try
        {
            return new MailAddress(value.Trim()).Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "A valid proposal recipient is required.",
                nameof(value),
                exception);
        }
    }

    private static string Required(string value, int maximum, string parameter)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximum)
        {
            throw new ArgumentException("A valid delivery value is required.", parameter);
        }
        return normalized;
    }
}
