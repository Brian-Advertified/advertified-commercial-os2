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

public interface IEmailProviderClient
{
    string ProviderCode { get; }

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
}

public interface IEmailProviderResolver
{
    IEmailProviderClient Resolve(string providerCode);
}
