using System.Net.Mail;
using System.Text;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed partial class EmailAutomationCommands
{
    private async Task<CommandOutcome> ReceiveOutcomeAsync(
        CommandEnvelope<ReceiveInboundEmailCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var mailbox = await store.FindMailboxAsync(envelope.TenantId, cancellationToken)
            ?? throw new InboundMailboxNotConfiguredException();
        if (mailbox.Id != command.MailboxId || mailbox.OwnerUserId != envelope.ActorId.Value ||
            !mailbox.IsEnabled)
        {
            throw new UnauthorizedAccessException("Inbound mailbox access denied.");
        }
        var sender = NormalizeAddress(command.SenderEmail, nameof(command.SenderEmail));
        var replyTo = NormalizeAddress(command.ReplyToEmail, nameof(command.ReplyToEmail));
        EnsureSenderAllowed(mailbox, sender);
        var providerEventId = Required(
            command.ProviderEventId, 300, nameof(command.ProviderEventId));
        var providerEmailId = Required(
            command.ProviderEmailId, 300, nameof(command.ProviderEmailId));
        var providerMessageId = Required(
            command.ProviderMessageId, 1_000, nameof(command.ProviderMessageId));
        var subject = Required(command.Subject, 1_000, nameof(command.Subject));
        var body = Required(command.BodyText, checked((int)policy.MaximumSourceBytes),
            nameof(command.BodyText));
        var sourceBytes = Encoding.UTF8.GetByteCount(string.Concat(subject, "\n", body));
        if (sourceBytes > policy.MaximumSourceBytes)
        {
            throw new ArgumentException("The inbound request is too large to process safely.");
        }
        ValidateJsonObject(command.RawMetadataJson);
        var attachments = PrepareAttachments(command.Attachments);
        var duplicate = await store.FindDuplicateAsync(
            envelope.TenantId,
            mailbox.Id,
            providerEventId,
            providerEmailId,
            providerMessageId,
            command.SourceHash,
            cancellationToken);
        if (duplicate is not null)
        {
            var existingRun = await store.FindRunAsync(
                envelope.TenantId, duplicate.Id, cancellationToken)
                ?? throw new InvalidOperationException("The duplicate email has no automation run.");
            var duplicateView = new InboundEmailReceiptView(
                duplicate.Id, existingRun.Id, existingRun.Status, true);
            return OpportunityCommandSupport.Outcome(
                envelope,
                duplicateView,
                duplicate.Id,
                1,
                MasterDataReferences.CommercialResourceTypes.InboundCampaignEmail,
                MasterDataReferences.CommercialActions.InboundEmailReceived,
                MasterDataReferences.CommercialEventTypes.InboundCampaignEmailReceived,
                timeProvider.GetUtcNow());
        }

        var emailId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inbound_campaign_emails (
                id, tenant_id, mailbox_id, provider_event_id, provider_email_id,
                provider_message_id, sender_email, sender_name, reply_to_email,
                subject, body_text, source_hash, raw_metadata_json,
                received_at_utc, created_at_utc)
            VALUES ({emailId}, {envelope.TenantId.Value}, {mailbox.Id},
                {providerEventId}, {providerEmailId}, {providerMessageId},
                {sender}, {NormalizeOptional(command.SenderName, 300)}, {replyTo},
                {subject}, {body}, {NormalizeHash(command.SourceHash)},
                {command.RawMetadataJson}::jsonb, {command.ReceivedAtUtc}, {now});
            INSERT INTO commercial.email_proposal_automation_runs (
                id, tenant_id, inbound_email_id, policy_version,
                campaign_mode_code, status_code, checkpoint_code,
                client_account_id, input_hash, delivery_provider_collection_code,
                delivery_provider_code, incremental_ai_cost_minor,
                version, created_at_utc, updated_at_utc)
            VALUES ({runId}, {envelope.TenantId.Value}, {emailId}, {policy.Version},
                {MasterDataCodes.CampaignModes.OohOnly},
                {MasterDataCodes.EmailAutomationStatuses.Received},
                {MasterDataCodes.EmailAutomationCheckpoints.SourceCaptured},
                {mailbox.DefaultClientAccountId}, {NormalizeHash(command.SourceHash)},
                {MasterDataCodes.EmailProviders.Collection}, {mailbox.Provider},
                0, 1, {now}, {now})
            """, cancellationToken);
        await InsertAttachmentsAsync(
            envelope.TenantId, emailId, attachments, now, cancellationToken);
        var view = new InboundEmailReceiptView(
            emailId,
            runId,
            MasterDataCodes.EmailAutomationStatuses.Received,
            false);
        return OpportunityCommandSupport.Outcome(
            envelope,
            view,
            emailId,
            1,
            MasterDataReferences.CommercialResourceTypes.InboundCampaignEmail,
            MasterDataReferences.CommercialActions.InboundEmailReceived,
            MasterDataReferences.CommercialEventTypes.InboundCampaignEmailReceived,
            now);
    }

    private Task<int> InsertAttachmentsAsync(
        TenantId tenantId,
        Guid emailId,
        PreparedInboundAttachment[] attachments,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (attachments.Length == 0) return Task.FromResult(0);
        var payload = EmailAutomationRecordStore.Write(attachments);
        return store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inbound_email_attachments (
                id, tenant_id, inbound_email_id, provider_attachment_id,
                file_name, media_type, size_bytes, created_at_utc)
            SELECT value."id", {tenantId.Value}, {emailId},
                value."providerAttachmentId", value."fileName", value."mediaType",
                value."sizeBytes", {now}
            FROM jsonb_to_recordset({payload}::jsonb) AS value(
                "id" uuid, "providerAttachmentId" text, "fileName" text,
                "mediaType" text, "sizeBytes" bigint)
            """, cancellationToken);
    }

    private PreparedInboundAttachment[] PrepareAttachments(
        IReadOnlyList<InboundAttachmentReference> attachments)
    {
        if (!policy.AllowAttachments && attachments.Count > 0 || attachments.Count > 50 ||
            attachments.Any(item => item.SizeBytes < 0) ||
            attachments.Sum(item => item.SizeBytes) > policy.MaximumSourceBytes)
        {
            throw new ArgumentException("Inbound email attachments exceed the automation limit.");
        }
        var prepared = attachments.Select(item => new PreparedInboundAttachment(
            Guid.NewGuid(),
            Required(item.ProviderAttachmentId, 300, nameof(item.ProviderAttachmentId)),
            Required(item.FileName, 500, nameof(item.FileName)),
            Required(item.MediaType, 200, nameof(item.MediaType)),
            item.SizeBytes)).ToArray();
        if (prepared.Select(item => item.ProviderAttachmentId)
            .Distinct(StringComparer.Ordinal).Count() != prepared.Length)
        {
            throw new ArgumentException("Inbound email attachment identifiers must be unique.");
        }
        return prepared;
    }

    private static void EnsureSenderAllowed(InboundMailboxRow mailbox, string sender)
    {
        var domains = EmailAutomationRecordStore.Read<string[]>(
            mailbox.AllowedSenderDomainsJson);
        if (domains.Length == 0)
        {
            return;
        }
        var domain = sender[(sender.LastIndexOf('@') + 1)..];
        if (!domains.Any(allowed => string.Equals(domain, allowed, StringComparison.Ordinal) ||
                domain.EndsWith(string.Concat(".", allowed), StringComparison.Ordinal)))
        {
            throw new UnauthorizedAccessException("The sender is not allowed for this mailbox.");
        }
    }

    private static string NormalizeHash(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character =>
                !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A valid SHA-256 source hash is required.");
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized.Length <= maximumLength
                ? normalized
                : throw new ArgumentException("An inbound email value is too long.");
    }
}

internal sealed record PreparedInboundAttachment(
    Guid Id,
    string ProviderAttachmentId,
    string FileName,
    string MediaType,
    long SizeBytes);
