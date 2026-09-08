namespace Advertified.Commercial.Application.Proposal;

public static class ProposalBrandingStatuses
{
    public const string Outstanding = "OUTSTANDING";
    public const string Ready = "BRANDING_READY";
    public const string UnbrandedApproved = "UNBRANDED_AUTHORISED";
}

public sealed record ProposalBrandDocument(
    string FileName,
    string MediaType,
    byte[] Content);

public sealed record UploadProposalBrandAssetCommand(
    Guid ProposalVersionId,
    bool ClientAsset,
    string Label,
    string SourceReference,
    ProposalBrandDocument Document);

public sealed record ApproveProposalBrandAssetCommand;

public sealed record ConfigureProposalBrandingCommand(
    Guid? AgencyBrandAssetId,
    Guid? ClientBrandAssetId,
    string? PrimaryColour,
    string? SecondaryColour);

public sealed record ApproveUnbrandedProposalCommand(string Reason);

public sealed record ProposalBrandAssetView(
    Guid Id,
    Guid? ClientAccountId,
    string Label,
    string MediaType,
    string FileName,
    string ContentHash,
    string SourceReference,
    Guid UploadedBy,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAtUtc,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record ProposalBrandingView(
    string Status,
    string AgencyName,
    string ClientBrandName,
    string? PrimaryColour,
    string? SecondaryColour,
    ProposalBrandAssetView? AgencyAsset,
    ProposalBrandAssetView? ClientAsset,
    Guid? UnbrandedApprovedBy,
    DateTimeOffset? UnbrandedApprovedAtUtc,
    string? UnbrandedApprovalReason);
