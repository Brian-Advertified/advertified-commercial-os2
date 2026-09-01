using System.Text.Json;
using Advertified.Commercial.Application.EmailAutomation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.Extensions.Options;

namespace Advertified.Commercial.Infrastructure.EmailAutomation;

public sealed class InboundEmailReceiver(
    EmailAutomationRecordStore store,
    IEmailProviderResolver providers,
    IEmailAutomationCommands commands,
    IEmailProposalAutomationProcessor processor,
    IOptions<EmailAutomationOptions> options,
    TimeProvider timeProvider) : IInboundEmailReceiver
{
    public async Task<InboundEmailReceiptView> ReceiveAsync(
        TenantId tenantId,
        InboundEmailNotification notification,
        string providerEventId,
        string rawPayload,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var provider = providers.Resolve(notification.Provider);
        var recipients = notification.Recipients
            .Select(EmailContentNormalizer.NormalizeAddress)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mailbox = await FindMailboxAsync(
            tenantId, provider.ProviderCode, recipients, cancellationToken)
            ?? throw new InboundMailboxNotConfiguredException();
        var email = await provider.RetrieveAsync(
            notification.ProviderEmailId, cancellationToken);
        ValidateRetrievedEmail(notification, email, mailbox.Address);
        var sender = EmailContentNormalizer.NormalizeAddress(email.SenderEmail);
        var replyTo = EmailContentNormalizer.SelectReplyAddress(email);
        var body = EmailContentNormalizer.Body(email);
        var sourceHash = EmailContentNormalizer.SourceHash(email, sender, replyTo, body);
        var allowedDomains = EmailAutomationRecordStore.Read<string[]>(
            mailbox.AllowedSenderDomainsJson);
        var automaticReply = EmailContentNormalizer.AssessAutomaticReply(
            provider.AssessInboundIdentity(email), sender, replyTo, allowedDomains);
        var metadata = EmailContentNormalizer.Metadata(
            rawPayload, email, automaticReply);
        var command = new ReceiveInboundEmailCommand(
            mailbox.Id,
            Required(providerEventId, 300),
            email.ProviderEmailId,
            email.ProviderMessageId,
            sender,
            email.SenderName,
            replyTo,
            email.Subject.Trim(),
            body,
            sourceHash,
            metadata,
            email.ReceivedAtUtc,
            email.Attachments);
        var now = timeProvider.GetUtcNow();
        var envelope = new CommandEnvelope<ReceiveInboundEmailCommand>(
            tenantId,
            new ActorId(mailbox.OwnerUserId),
            new CommandId(Guid.NewGuid()),
            correlationId,
            new IdempotencyKey(BuildIdempotencyKey(provider.ProviderCode, providerEventId)),
            new Sha256Digest(OpportunityCommandSupport.Hash(
                JsonSerializer.Serialize(command))),
            0,
            now,
            command);
        var result = await commands.ReceiveAsync(envelope, cancellationToken);
        if (result.Data.Duplicate ||
            !options.Value.ProcessInline ||
            !mailbox.AutoSendEnabled)
        {
            return result.Data;
        }
        var run = await processor.ProcessAsync(
            tenantId,
            new ActorId(mailbox.OwnerUserId),
            result.Data.InboundEmailId,
            correlationId,
            cancellationToken);
        return result.Data with { Status = run.Status };
    }

    private async Task<InboundMailboxRow?> FindMailboxAsync(
        TenantId tenantId,
        string provider,
        string[] recipients,
        CancellationToken cancellationToken)
    {
        await using var transaction = await store.BeginSessionAsync(
            new ActorId(Guid.NewGuid()), tenantId, cancellationToken);
        var mailbox = await store.FindMailboxByRecipientsAsync(
            tenantId, provider, recipients, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mailbox;
    }

    private static void ValidateRetrievedEmail(
        InboundEmailNotification notification,
        RetrievedInboundEmail email,
        string mailboxAddress)
    {
        var recipientSet = email.Recipients
            .Select(EmailContentNormalizer.NormalizeAddress)
            .ToHashSet(StringComparer.Ordinal);
        if (!string.Equals(notification.ProviderEmailId, email.ProviderEmailId,
                StringComparison.Ordinal) ||
            !string.Equals(notification.ProviderMessageId, email.ProviderMessageId,
                StringComparison.Ordinal) ||
            !recipientSet.Contains(mailboxAddress))
        {
            throw new InvalidEmailWebhookException();
        }
    }

    private static string BuildIdempotencyKey(string provider, string eventId)
    {
        var digest = OpportunityCommandSupport.Hash(string.Concat(provider, ":", eventId));
        return string.Concat("inbound-email-", digest);
    }

    private static string Required(string value, int maximumLength)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new InvalidEmailWebhookException();
        }
        return normalized;
    }
}
