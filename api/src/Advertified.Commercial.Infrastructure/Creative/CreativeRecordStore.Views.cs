using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed partial class CreativeRecordStore
{
    internal Task<List<CreativeWorkspaceRow>> ListWorkspaceRowsAsync(
        Guid campaignId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<CreativeWorkspaceRow>($"""
            SELECT requirement.id AS "RequirementId", requirement.campaign_id AS "CampaignId",
                requirement.booking_id AS "BookingId",
                requirement.media_plan_line_id AS "MediaPlanLineId",
                requirement.supplier_tenant_id AS "SupplierTenantId",
                requirement.channel_code AS "Channel",
                requirement.flight_start AS "FlightStart",
                requirement.flight_end AS "FlightEnd",
                requirement.format_code AS "FormatCode", requirement.width AS "Width",
                requirement.height AS "Height",
                requirement.required_media_type AS "RequiredMediaType",
                requirement.maximum_bytes AS "MaximumBytes",
                requirement.instructions AS "Instructions",
                asset.id AS "AssetId", asset.version AS "AssetVersion",
                version.id AS "VersionId", version.version_number AS "VersionNumber",
                version.file_name AS "FileName", version.media_type AS "MediaType",
                version.size_bytes AS "SizeBytes", version.content_sha256 AS "ContentSha256",
                version.approved_copy AS "ApprovedCopy",
                version.commercial_snapshot_json::text AS "CommercialSnapshotJson",
                version.created_by AS "VersionCreatedBy",
                version.created_at_utc AS "VersionCreatedAtUtc",
                brand.decision_code AS "BrandDecision",
                brand.rights_status_code AS "BrandRightsStatus",
                brand.evidence_reference AS "BrandEvidenceReference",
                brand.reason AS "BrandReason", brand.reviewed_by AS "BrandReviewedBy",
                brand.reviewer_tenant_id AS "BrandReviewerTenantId",
                brand.reviewed_at_utc AS "BrandReviewedAtUtc",
                supplier.decision_code AS "SupplierDecision",
                supplier.evidence_reference AS "SupplierEvidenceReference",
                supplier.reason AS "SupplierReason",
                supplier.reviewed_by AS "SupplierReviewedBy",
                supplier.reviewer_tenant_id AS "SupplierReviewerTenantId",
                supplier.reviewed_at_utc AS "SupplierReviewedAtUtc"
            FROM commercial.creative_requirements requirement
            LEFT JOIN commercial.creative_assets asset
              ON asset.buyer_tenant_id = requirement.buyer_tenant_id
             AND asset.requirement_id = requirement.id
            LEFT JOIN commercial.creative_asset_versions version
              ON version.buyer_tenant_id = asset.buyer_tenant_id
             AND version.id = asset.current_version_id
            LEFT JOIN commercial.creative_asset_reviews brand
              ON brand.buyer_tenant_id = asset.buyer_tenant_id
             AND brand.asset_version_id = version.id
             AND brand.review_type_code = {MasterDataCodes.CreativeReviewTypes.BrandLegalRights}
            LEFT JOIN commercial.creative_asset_reviews supplier
              ON supplier.buyer_tenant_id = asset.buyer_tenant_id
             AND supplier.asset_version_id = version.id
             AND supplier.review_type_code = {MasterDataCodes.CreativeReviewTypes.SupplierTechnical}
            WHERE requirement.campaign_id = {campaignId}
            ORDER BY requirement.id
            """).ToListAsync(cancellationToken);

    internal async Task<CreativeAssetView> GetAssetViewAsync(
        Guid campaignId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var rows = await ListWorkspaceRowsAsync(campaignId, cancellationToken);
        return rows.Select(item => item.ToView().Asset)
            .SingleOrDefault(item => item?.Id == assetId)
            ?? throw new UnauthorizedAccessException("Creative asset access denied.");
    }

    internal Task<SupplierCreativeRow?> FindSupplierViewAsync(
        Guid assetId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<SupplierCreativeRow>($"""
            SELECT asset.id AS "AssetId", asset.campaign_id AS "CampaignId",
                requirement.id AS "RequirementId", requirement.channel_code AS "Channel",
                requirement.format_code AS "FormatCode", requirement.width AS "Width",
                requirement.height AS "Height",
                requirement.required_media_type AS "RequiredMediaType",
                requirement.maximum_bytes AS "MaximumBytes",
                requirement.instructions AS "Instructions",
                version.id AS "VersionId", version.version_number AS "VersionNumber",
                version.file_name AS "FileName", version.media_type AS "MediaType",
                version.size_bytes AS "SizeBytes", version.content_sha256 AS "ContentSha256",
                review.decision_code AS "SupplierDecision", asset.version AS "Version"
            FROM commercial.creative_assets asset
            JOIN commercial.creative_requirements requirement
              ON requirement.buyer_tenant_id = asset.buyer_tenant_id
             AND requirement.id = asset.requirement_id
            JOIN commercial.creative_asset_versions version
              ON version.buyer_tenant_id = asset.buyer_tenant_id
             AND version.id = asset.current_version_id
            LEFT JOIN commercial.creative_asset_reviews review
              ON review.buyer_tenant_id = asset.buyer_tenant_id
             AND review.asset_version_id = version.id
             AND review.review_type_code = {MasterDataCodes.CreativeReviewTypes.SupplierTechnical}
            WHERE asset.id = {assetId}
            """).SingleOrDefaultAsync(cancellationToken);

    internal static CreativeWorkspaceView ToWorkspace(
        IReadOnlyList<CreativeWorkspaceRow> rows)
    {
        var requirements = rows.Select(item => item.ToView()).ToArray();
        var ready = requirements.Length > 0 && requirements.All(item =>
            item.Asset?.CurrentVersion.BrandReview is
                { Decision: MasterDataCodes.LifecycleStatuses.Approved,
                  RightsStatus: MasterDataCodes.AssetRightsStatuses.Approved } &&
            item.Asset.CurrentVersion.SupplierReview?.Decision ==
                MasterDataCodes.LifecycleStatuses.Approved);
        return new CreativeWorkspaceView(ready, requirements);
    }
}
