using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.EmailAutomation;

public sealed record ConfigureInboundMailboxCommand(
    string Address,
    string Provider,
    Guid OwnerUserId,
    Guid? DefaultClientAccountId,
    bool AutoSendEnabled,
    IReadOnlyList<string> AllowedSenderDomains);

public sealed record ReceiveInboundEmailCommand(
    Guid MailboxId,
    string ProviderEventId,
    string ProviderEmailId,
    string ProviderMessageId,
    string SenderEmail,
    string? SenderName,
    string ReplyToEmail,
    string Subject,
    string BodyText,
    string SourceHash,
    string RawMetadataJson,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<InboundAttachmentReference> Attachments);

public sealed record ProcessInboundEmailCommand;

public sealed record EmailAutomationClarificationInput(
    string FieldPath,
    string Value);

public sealed record RetryInboundEmailCommand(
    string Reason,
    IReadOnlyList<EmailAutomationClarificationInput>? Clarifications = null);

public interface IEmailAutomationCommands
{
    Task<CommandResult<InboundMailboxView>> ConfigureMailboxAsync(
        CommandEnvelope<ConfigureInboundMailboxCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InboundEmailReceiptView>> ReceiveAsync(
        CommandEnvelope<ReceiveInboundEmailCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EmailAutomationRunView>> ProcessAsync(
        Guid inboundEmailId,
        CommandEnvelope<ProcessInboundEmailCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<EmailAutomationRunView>> RetryAsync(
        Guid inboundEmailId,
        CommandEnvelope<RetryInboundEmailCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IInboundEmailReceiver
{
    Task<InboundEmailReceiptView> ReceiveAsync(
        TenantId tenantId,
        InboundEmailNotification notification,
        string providerEventId,
        string rawPayload,
        CorrelationId correlationId,
        CancellationToken cancellationToken);
}

public interface IEmailProposalAutomationProcessor
{
    Task<EmailAutomationRunView> ProcessAsync(
        TenantId tenantId,
        ActorId actorId,
        Guid inboundEmailId,
        CorrelationId correlationId,
        CancellationToken cancellationToken);
}
