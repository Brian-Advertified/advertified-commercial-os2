namespace Advertified.Commercial.Infrastructure.Proposal;

internal sealed record ProposalRow(
    Guid Id,
    Guid BriefId,
    Guid BriefVersionId,
    int VersionNumber,
    string Title,
    string ExecutiveSummary,
    string Terms,
    DateTimeOffset ExpiryAtUtc,
    string Status,
    string InputHash,
    Guid CreatedBy,
    Guid? ApprovedBy,
    Guid? RecipientUserId,
    long Version,
    DateTimeOffset CreatedAtUtc);

internal sealed record ProposalOptionRow(
    Guid Id,
    Guid PlanVersionId,
    int PlanVersionNumber,
    string Label,
    string Outcome,
    long BudgetMinor,
    string Currency,
    int DisplayOrder,
    string PlanSignature,
    string ChannelsJson,
    string RunningPeriodsJson,
    string InventoryJson);

internal sealed record ProposalDocumentRow(
    Guid Id,
    Guid ProposalVersionId,
    string MediaType,
    string FileName,
    string ContentHash,
    byte[] Content,
    DateTimeOffset CreatedAtUtc);

internal sealed record ProposalDecisionRow(
    string Decision,
    Guid? OptionId,
    string? Reason,
    Guid DecidedBy,
    DateTimeOffset DecidedAtUtc);

internal sealed record ApprovedBriefReferenceRow(
    Guid BriefId,
    Guid BriefVersionId,
    string Objective,
    Guid OwnerUserId,
    long BriefVersion,
    string EvidenceIdsJson);

internal sealed record ProposalRecipientRow(
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    string Status);
