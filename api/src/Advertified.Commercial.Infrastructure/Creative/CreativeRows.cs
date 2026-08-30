using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Creative;

internal sealed record CreativeBookingSourceRow(
    Guid BookingId,
    Guid SupplierTenantId,
    Guid MediaPlanLineId,
    string Channel,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    long BookingVersion,
    string Currency,
    long SupplierCostMinor,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor);

internal sealed record CreativeRequirementSourceRow(
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
    long CampaignVersion,
    long BookingVersion,
    string Currency,
    long SupplierCostMinor,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor);

internal sealed record CreativeAssetStateRow(
    Guid Id,
    Guid BuyerTenantId,
    Guid CampaignId,
    Guid RequirementId,
    Guid SupplierTenantId,
    Guid? CurrentVersionId,
    int? CurrentVersionNumber,
    long Version);

internal sealed record CreativeWorkspaceRow(
    Guid RequirementId,
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
    Guid? AssetId,
    long? AssetVersion,
    Guid? VersionId,
    int? VersionNumber,
    string? FileName,
    string? MediaType,
    long? SizeBytes,
    string? ContentSha256,
    string? ApprovedCopy,
    string? CommercialSnapshotJson,
    Guid? VersionCreatedBy,
    DateTimeOffset? VersionCreatedAtUtc,
    string? BrandDecision,
    string? BrandRightsStatus,
    string? BrandEvidenceReference,
    string? BrandReason,
    Guid? BrandReviewedBy,
    Guid? BrandReviewerTenantId,
    DateTimeOffset? BrandReviewedAtUtc,
    string? SupplierDecision,
    string? SupplierEvidenceReference,
    string? SupplierReason,
    Guid? SupplierReviewedBy,
    Guid? SupplierReviewerTenantId,
    DateTimeOffset? SupplierReviewedAtUtc)
{
    internal CreativeRequirementView ToView() => new(
        RequirementId, CampaignId, BookingId, MediaPlanLineId, SupplierTenantId,
        Channel, FlightStart, FlightEnd, FormatCode, Width, Height,
        RequiredMediaType, MaximumBytes, Instructions, AssetView());

    private CreativeAssetView? AssetView()
    {
        if (AssetId is null || AssetVersion is null || VersionId is null ||
            VersionNumber is null || FileName is null || MediaType is null ||
            SizeBytes is null || ContentSha256 is null || ApprovedCopy is null ||
            CommercialSnapshotJson is null || VersionCreatedBy is null ||
            VersionCreatedAtUtc is null)
            return null;
        var version = new CreativeAssetVersionView(
            VersionId.Value, VersionNumber.Value, FileName, MediaType, SizeBytes.Value,
            ContentSha256, ApprovedCopy, CommercialSnapshotJson, VersionCreatedBy.Value,
            VersionCreatedAtUtc.Value, BrandReview(), SupplierReview());
        return new CreativeAssetView(
            AssetId.Value, RequirementId, AssetVersion.Value, version);
    }

    private CreativeReviewView? BrandReview() => BrandDecision is null ||
        BrandEvidenceReference is null || BrandReason is null || BrandReviewedBy is null ||
        BrandReviewerTenantId is null || BrandReviewedAtUtc is null
        ? null : new(
            MasterDataCodes.CreativeReviewTypes.BrandLegalRights,
            BrandDecision, BrandRightsStatus,
            BrandEvidenceReference, BrandReason, BrandReviewedBy.Value,
            BrandReviewerTenantId.Value, BrandReviewedAtUtc.Value);

    private CreativeReviewView? SupplierReview() => SupplierDecision is null ||
        SupplierEvidenceReference is null || SupplierReason is null ||
        SupplierReviewedBy is null || SupplierReviewerTenantId is null ||
        SupplierReviewedAtUtc is null
        ? null : new(
            MasterDataCodes.CreativeReviewTypes.SupplierTechnical,
            SupplierDecision, null,
            SupplierEvidenceReference, SupplierReason, SupplierReviewedBy.Value,
            SupplierReviewerTenantId.Value, SupplierReviewedAtUtc.Value);
}

internal sealed record SupplierCreativeRow(
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
    long Version)
{
    internal SupplierCreativeAssetView ToView() => new(
        AssetId, CampaignId, RequirementId, Channel, FormatCode, Width, Height,
        RequiredMediaType, MaximumBytes, Instructions, VersionId, VersionNumber,
        FileName, MediaType, SizeBytes, ContentSha256, SupplierDecision, Version);
}

internal sealed record PreparedCreativeRequirement(
    Guid Id,
    CreativeBookingSourceRow Source,
    string FormatCode,
    int Width,
    int Height,
    string RequiredMediaType,
    int MaximumBytes,
    string Instructions);
