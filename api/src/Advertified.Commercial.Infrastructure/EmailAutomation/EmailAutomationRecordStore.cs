using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationRecordStore(GovernanceDbContext dbContext)
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

    internal Task<InboundMailboxRow?> FindMailboxAsync(
        TenantId tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InboundMailboxRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", address AS "Address",
                provider_code AS "Provider", owner_user_id AS "OwnerUserId",
                default_client_account_id AS "DefaultClientAccountId",
                auto_send_enabled AS "AutoSendEnabled",
                allowed_sender_domains_json::text AS "AllowedSenderDomainsJson",
                is_enabled AS "IsEnabled", version AS "Version",
                created_at_utc AS "CreatedAtUtc", updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.inbound_mailboxes
            WHERE tenant_id = {tenantId.Value}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InboundMailboxRow?> FindMailboxByRecipientsAsync(
        TenantId tenantId,
        string provider,
        string[] recipients,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InboundMailboxRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", address AS "Address",
                provider_code AS "Provider", owner_user_id AS "OwnerUserId",
                default_client_account_id AS "DefaultClientAccountId",
                auto_send_enabled AS "AutoSendEnabled",
                allowed_sender_domains_json::text AS "AllowedSenderDomainsJson",
                is_enabled AS "IsEnabled", version AS "Version",
                created_at_utc AS "CreatedAtUtc", updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.inbound_mailboxes
            WHERE tenant_id = {tenantId.Value}
              AND provider_code = {provider}
              AND address = ANY({recipients})
              AND is_enabled
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InboundCampaignEmailRow?> FindEmailAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InboundCampaignEmailRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", mailbox_id AS "MailboxId",
                provider_event_id AS "ProviderEventId",
                provider_email_id AS "ProviderEmailId",
                provider_message_id AS "ProviderMessageId",
                sender_email AS "SenderEmail", sender_name AS "SenderName",
                reply_to_email AS "ReplyToEmail", subject AS "Subject",
                body_text AS "BodyText", source_hash AS "SourceHash",
                raw_metadata_json::text AS "RawMetadataJson",
                received_at_utc AS "ReceivedAtUtc", created_at_utc AS "CreatedAtUtc"
            FROM commercial.inbound_campaign_emails
            WHERE tenant_id = {tenantId.Value} AND id = {inboundEmailId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<InboundCampaignEmailRow?> FindDuplicateAsync(
        TenantId tenantId,
        Guid mailboxId,
        string providerEventId,
        string providerEmailId,
        string providerMessageId,
        string sourceHash,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InboundCampaignEmailRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", mailbox_id AS "MailboxId",
                provider_event_id AS "ProviderEventId",
                provider_email_id AS "ProviderEmailId",
                provider_message_id AS "ProviderMessageId",
                sender_email AS "SenderEmail", sender_name AS "SenderName",
                reply_to_email AS "ReplyToEmail", subject AS "Subject",
                body_text AS "BodyText", source_hash AS "SourceHash",
                raw_metadata_json::text AS "RawMetadataJson",
                received_at_utc AS "ReceivedAtUtc", created_at_utc AS "CreatedAtUtc"
            FROM commercial.inbound_campaign_emails
            WHERE tenant_id = {tenantId.Value} AND mailbox_id = {mailboxId}
              AND (provider_event_id = {providerEventId}
                OR provider_email_id = {providerEmailId}
                OR provider_message_id = {providerMessageId}
                OR source_hash = {sourceHash})
            ORDER BY created_at_utc, id
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<EmailAutomationRunRow?> FindRunAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<EmailAutomationRunRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId",
                inbound_email_id AS "InboundEmailId", policy_version AS "PolicyVersion",
                campaign_mode_code AS "CampaignMode", status_code AS "Status",
                checkpoint_code AS "Checkpoint", client_account_id AS "ClientAccountId",
                brief_id AS "BriefId", brief_version_id AS "BriefVersionId",
                stp_version_id AS "StpVersionId",
                media_mix_version_id AS "MediaMixVersionId",
                shortlist_version_id AS "ShortlistVersionId",
                media_plan_version_id AS "MediaPlanVersionId",
                proposal_version_id AS "ProposalVersionId", document_id AS "DocumentId",
                input_hash AS "InputHash", understanding_json::text AS "UnderstandingJson",
                clarifications_json::text AS "ClarificationsJson",
                failure_code AS "FailureCode",
                failure_message AS "FailureMessage",
                delivery_idempotency_key AS "DeliveryIdempotencyKey",
                delivery_provider_id AS "DeliveryProviderId",
                incremental_ai_cost_minor AS "IncrementalAiCostMinor",
                version AS "Version", created_at_utc AS "CreatedAtUtc",
                updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.email_proposal_automation_runs
            WHERE tenant_id = {tenantId.Value} AND inbound_email_id = {inboundEmailId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<EmailAutomationContextRow?> FindContextAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<EmailAutomationContextRow>($"""
            SELECT email.id AS "InboundEmailId", email.tenant_id AS "TenantId",
                email.mailbox_id AS "MailboxId", mailbox.address AS "MailboxAddress",
                mailbox.provider_code AS "Provider", mailbox.owner_user_id AS "OwnerUserId",
                mailbox.default_client_account_id AS "DefaultClientAccountId",
                mailbox.auto_send_enabled AS "AutoSendEnabled",
                mailbox.allowed_sender_domains_json::text AS "AllowedSenderDomainsJson",
                mailbox.is_enabled AS "MailboxEnabled",
                email.provider_email_id AS "ProviderEmailId",
                email.provider_message_id AS "ProviderMessageId",
                email.sender_email AS "SenderEmail", email.reply_to_email AS "ReplyToEmail",
                email.subject AS "Subject", email.body_text AS "BodyText",
                email.source_hash AS "SourceHash",
                email.raw_metadata_json::text AS "RawMetadataJson",
                email.received_at_utc AS "ReceivedAtUtc",
                (SELECT COUNT(*)::integer
                 FROM commercial.inbound_email_attachments attachment
                 WHERE attachment.tenant_id = email.tenant_id
                   AND attachment.inbound_email_id = email.id) AS "AttachmentCount",
                run.id AS "RunId",
                run.status_code AS "RunStatus", run.checkpoint_code AS "Checkpoint",
                run.version AS "RunVersion", run.understanding_json::text AS "UnderstandingJson",
                run.clarifications_json::text AS "ClarificationsJson",
                run.client_account_id AS "ClientAccountId",
                run.brief_id AS "BriefId", run.brief_version_id AS "BriefVersionId",
                run.stp_version_id AS "StpVersionId",
                run.media_mix_version_id AS "MediaMixVersionId",
                run.shortlist_version_id AS "ShortlistVersionId",
                run.media_plan_version_id AS "MediaPlanVersionId",
                run.proposal_version_id AS "ProposalVersionId",
                run.document_id AS "DocumentId",
                run.delivery_idempotency_key AS "DeliveryIdempotencyKey",
                run.delivery_provider_id AS "DeliveryProviderId",
                run.incremental_ai_cost_minor AS "IncrementalAiCostMinor"
            FROM commercial.inbound_campaign_emails email
            JOIN commercial.inbound_mailboxes mailbox
              ON mailbox.tenant_id = email.tenant_id AND mailbox.id = email.mailbox_id
            JOIN commercial.email_proposal_automation_runs run
              ON run.tenant_id = email.tenant_id AND run.inbound_email_id = email.id
            WHERE email.tenant_id = {tenantId.Value} AND email.id = {inboundEmailId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<List<InboundAttachmentRow>> ListAttachmentsAsync(
        TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InboundAttachmentRow>($"""
            SELECT id AS "Id", inbound_email_id AS "InboundEmailId",
                provider_attachment_id AS "ProviderAttachmentId",
                file_name AS "FileName", media_type AS "MediaType",
                size_bytes AS "SizeBytes"
            FROM commercial.inbound_email_attachments
            WHERE tenant_id = {tenantId.Value} AND inbound_email_id = {inboundEmailId}
            ORDER BY file_name, id
            """).ToListAsync(cancellationToken);

    internal Task<List<InboundCampaignEmailRow>> ListEmailsAsync(
        TenantId tenantId,
        int pageSize,
        DateTimeOffset? before,
        CancellationToken cancellationToken)
    {
        var statement = FormattableStringFactory.Create(
            """
            SELECT id AS "Id", tenant_id AS "TenantId", mailbox_id AS "MailboxId",
                provider_event_id AS "ProviderEventId",
                provider_email_id AS "ProviderEmailId",
                provider_message_id AS "ProviderMessageId",
                sender_email AS "SenderEmail", sender_name AS "SenderName",
                reply_to_email AS "ReplyToEmail", subject AS "Subject",
                body_text AS "BodyText", source_hash AS "SourceHash",
                raw_metadata_json::text AS "RawMetadataJson",
                received_at_utc AS "ReceivedAtUtc", created_at_utc AS "CreatedAtUtc"
            FROM commercial.inbound_campaign_emails
            WHERE tenant_id = {0} AND ({1}::timestamptz IS NULL OR received_at_utc < {1})
            ORDER BY received_at_utc DESC, id DESC LIMIT {2}
            """,
            tenantId.Value, before, pageSize + 1);
        return dbContext.Database.SqlQuery<InboundCampaignEmailRow>(statement)
            .ToListAsync(cancellationToken);
    }
}
