namespace Advertified.Commercial.Application.EmailAutomation;

public sealed record InboundEmailNotification(
    string Provider,
    string ProviderEmailId,
    string ProviderMessageId,
    IReadOnlyList<string> Recipients,
    string Sender,
    string Subject,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<InboundAttachmentReference> Attachments);

public sealed record InboundAttachmentReference(
    string ProviderAttachmentId,
    string FileName,
    string MediaType,
    long SizeBytes);

public sealed record RetrievedInboundEmail(
    string ProviderEmailId,
    string ProviderMessageId,
    IReadOnlyList<string> Recipients,
    string SenderEmail,
    string? SenderName,
    IReadOnlyList<string> ReplyTo,
    string Subject,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<InboundAttachmentReference> Attachments,
    DateTimeOffset ReceivedAtUtc);

public sealed record InboundEmailIdentityAssessment(bool SenderAuthenticated);

public sealed record ProposalEmailDelivery(
    string To,
    string From,
    string Subject,
    string TextBody,
    string FileName,
    string MediaType,
    byte[] Attachment,
    string? InReplyTo,
    string IdempotencyKey);

public sealed record EmailDeliveryReceipt(
    string ProviderMessageId,
    DateTimeOffset AcceptedAtUtc);

public enum EmailDeliveryReconciliationOutcome
{
    Accepted,
    NotFound,
    Unknown
}

public sealed record EmailDeliveryReconciliationResult(
    EmailDeliveryReconciliationOutcome Outcome,
    EmailDeliveryReceipt? Receipt);

public interface IEmailProviderClient
{
    string ProviderCode { get; }

    InboundEmailIdentityAssessment AssessInboundIdentity(
        RetrievedInboundEmail email);

    bool VerifyWebhook(
        string rawPayload,
        string messageId,
        string timestamp,
        string signature,
        DateTimeOffset now);

    InboundEmailNotification ParseNotification(string rawPayload);

    Task<RetrievedInboundEmail> RetrieveAsync(
        string providerEmailId,
        CancellationToken cancellationToken);

    Task<EmailDeliveryReceipt> SendAsync(
        ProposalEmailDelivery delivery,
        CancellationToken cancellationToken);

    Task<EmailDeliveryReconciliationResult> ReconcileDeliveryAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface IEmailProviderResolver
{
    IEmailProviderClient Resolve(string providerCode);
}
