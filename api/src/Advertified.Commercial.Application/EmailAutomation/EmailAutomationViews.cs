namespace Advertified.Commercial.Application.EmailAutomation;

public sealed record InboundMailboxView(
    Guid Id,
    Guid TenantId,
    string Address,
    string Provider,
    Guid OwnerUserId,
    Guid? DefaultClientAccountId,
    bool AutoSendEnabled,
    IReadOnlyList<string> AllowedSenderDomains,
    bool IsEnabled,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record InboundAttachmentView(
    string ProviderAttachmentId,
    string FileName,
    string MediaType,
    long SizeBytes);

public sealed record InboundCampaignEmailView(
    Guid Id,
    Guid TenantId,
    Guid MailboxId,
    string ProviderEmailId,
    string ProviderMessageId,
    string SenderEmail,
    string? SenderName,
    string ReplyToEmail,
    string Subject,
    string SourceHash,
    IReadOnlyList<InboundAttachmentView> Attachments,
    string Status,
    string? FailureCode,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmailAutomationRunView(
    Guid Id,
    Guid TenantId,
    Guid InboundEmailId,
    string CampaignMode,
    string Status,
    string Checkpoint,
    Guid? ClientAccountId,
    Guid? BriefId,
    Guid? BriefVersionId,
    Guid? StpVersionId,
    Guid? MediaMixVersionId,
    Guid? ShortlistVersionId,
    Guid? MediaPlanVersionId,
    Guid? ProposalVersionId,
    Guid? DocumentId,
    string? FailureCode,
    string? FailureMessage,
    string? DeliveryProviderId,
    long IncrementalAiCostMinor,
    long Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record EmailAutomationQuestionView(
    string FieldPath,
    string Question,
    IReadOnlyList<string> Options);

public sealed record InboundEmailDetailView(
    InboundCampaignEmailView Email,
    EmailAutomationRunView Run,
    string SourceContent,
    IReadOnlyList<EmailAutomationQuestionView> Questions);

public sealed record InboundEmailPage(
    IReadOnlyList<InboundCampaignEmailView> Items,
    string? NextCursor);

public sealed record InboundEmailReceiptView(
    Guid InboundEmailId,
    Guid AutomationRunId,
    string Status,
    bool Duplicate);

public interface IEmailAutomationReader
{
    Task<InboundMailboxView?> GetMailboxAsync(
        Advertified.Commercial.Domain.Governance.ActorId actorId,
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        CancellationToken cancellationToken);

    Task<InboundEmailPage> ListAsync(
        Advertified.Commercial.Domain.Governance.ActorId actorId,
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken);

    Task<InboundEmailDetailView> GetAsync(
        Advertified.Commercial.Domain.Governance.ActorId actorId,
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        Guid inboundEmailId,
        CancellationToken cancellationToken);
}
