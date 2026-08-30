using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Creative;

public sealed record CreativeRequirementInput(
    Guid BookingId,
    string FormatCode,
    int Width,
    int Height,
    string RequiredMediaType,
    int MaximumBytes,
    string Instructions);

public sealed record RequestCampaignCreativeCommand(
    IReadOnlyList<CreativeRequirementInput> Requirements,
    string Reason);

public sealed record CreativeFileUpload(
    string FileName,
    string MediaType,
    byte[] Content);

public sealed record CreateCreativeAssetCommand(
    long CampaignVersion,
    Guid RequirementId,
    string ApprovedCopy,
    CreativeFileUpload File);

public sealed record UploadCreativeAssetVersionCommand(
    string ApprovedCopy,
    CreativeFileUpload File);

public sealed record ReviewCreativeBrandCommand(
    bool Approved,
    string RightsStatus,
    string EvidenceReference,
    string Reason);

public sealed record ReviewCreativeSupplierCommand(
    bool Approved,
    string EvidenceReference,
    string Reason);

public sealed record ApproveCampaignCreativeCommand(string Reason);

public sealed record CreativeReviewView(
    string ReviewType,
    string Decision,
    string? RightsStatus,
    string EvidenceReference,
    string Reason,
    Guid ReviewedBy,
    Guid ReviewerTenantId,
    DateTimeOffset ReviewedAtUtc);

public sealed record CreativeAssetVersionView(
    Guid Id,
    int VersionNumber,
    string FileName,
    string MediaType,
    long SizeBytes,
    string ContentSha256,
    string ApprovedCopy,
    string CommercialSnapshotJson,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    CreativeReviewView? BrandReview,
    CreativeReviewView? SupplierReview);

public sealed record CreativeAssetView(
    Guid Id,
    Guid RequirementId,
    long Version,
    CreativeAssetVersionView CurrentVersion);

public sealed record CreativeRequirementView(
    Guid Id,
    Guid CampaignId,
    Guid BookingId,
    Guid MediaPlanLineId,
    Guid SupplierTenantId,
    string Channel,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    string FormatCode,
    int Width,
    int Height,
    string RequiredMediaType,
    int MaximumBytes,
    string Instructions,
    CreativeAssetView? Asset);

public sealed record CreativeWorkspaceView(
    bool ReadyForApproval,
    IReadOnlyList<CreativeRequirementView> Requirements);

public sealed record SupplierCreativeAssetView(
    Guid AssetId,
    Guid CampaignId,
    Guid RequirementId,
    string Channel,
    string FormatCode,
    int Width,
    int Height,
    string RequiredMediaType,
    int MaximumBytes,
    string Instructions,
    Guid VersionId,
    int VersionNumber,
    string FileName,
    string MediaType,
    long SizeBytes,
    string ContentSha256,
    string? SupplierDecision,
    long Version);

public interface ICreativeCommands
{
    Task<CommandResult<CampaignView>> RequestAsync(
        Guid campaignId,
        CommandEnvelope<RequestCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CreativeAssetView>> CreateAssetAsync(
        Guid campaignId,
        CommandEnvelope<CreateCreativeAssetCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CreativeAssetView>> UploadVersionAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<UploadCreativeAssetVersionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CreativeAssetView>> ReviewBrandAsync(
        Guid campaignId,
        Guid assetId,
        CommandEnvelope<ReviewCreativeBrandCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<SupplierCreativeAssetView>> ReviewSupplierAsync(
        Guid assetId,
        CommandEnvelope<ReviewCreativeSupplierCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CampaignView>> ApproveCampaignAsync(
        Guid campaignId,
        CommandEnvelope<ApproveCampaignCreativeCommand> envelope,
        CancellationToken cancellationToken);
}

public interface ICreativeReader
{
    Task<CreativeWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid campaignId,
        CancellationToken cancellationToken);

    Task<SupplierCreativeAssetView> GetSupplierAssetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid assetId,
        CancellationToken cancellationToken);
}

public sealed class CreativeReadinessBlockedException : Exception;
public sealed class CreativeFileRejectedException : Exception;
