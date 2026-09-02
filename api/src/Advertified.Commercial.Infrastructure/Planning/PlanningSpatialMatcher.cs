using System.Text.Json;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Planning;

public sealed partial class PlanningRecordStore
{
    internal async Task<IReadOnlyDictionary<PlanningInventoryKey, InventorySpatialMatchView>>
        EvaluateSpatialMatchesAsync(
            TenantId tenantId,
            Guid briefVersionId,
            IReadOnlyList<PlanningInventoryRow> inventory,
            CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(inventory.Select(item => new
        {
            inventoryTenantId = item.InventoryTenantId,
            listingVersionId = item.MarketplaceListingVersionId,
            productVersionId = item.ProductVersionId,
        }));
        var rows = await DbContext.Database.SqlQuery<PlanningSpatialMatchRow>($"""
            WITH requested AS (
                SELECT value.*
                FROM jsonb_to_recordset({payload}::jsonb) AS value(
                    "inventoryTenantId" uuid, "listingVersionId" uuid,
                    "productVersionId" uuid)
            ), candidate_geometry AS (
                SELECT requested."inventoryTenantId" AS inventory_tenant_id,
                    requested."listingVersionId" AS listing_version_id,
                    requested."productVersionId" AS product_version_id,
                    COALESCE(direct.spatial_location,
                        listing.private_spatial_location) AS point_location,
                    COALESCE(direct.coverage_geometry,
                        listing.private_coverage_geometry) AS coverage_geometry,
                    COALESCE(direct.catchment_geometry,
                        listing.private_catchment_geometry) AS catchment_geometry,
                    COALESCE(direct.route_geometry,
                        listing.private_route_geometry) AS route_geometry
                FROM requested
                LEFT JOIN commercial.inventory_product_versions direct
                  ON requested."listingVersionId" IS NULL
                 AND direct.tenant_id = requested."inventoryTenantId"
                 AND direct.id = requested."productVersionId"
                LEFT JOIN commercial.marketplace_listing_versions listing
                  ON requested."listingVersionId" IS NOT NULL
                 AND listing.supplier_tenant_id = requested."inventoryTenantId"
                 AND listing.id = requested."listingVersionId"
            ), requirements AS (
                SELECT requirement.*,
                    CASE requirement.requirement_type_code
                        WHEN {MasterDataCodes.SpatialRequirementTypes.PointRadius} THEN
                            ST_Buffer(requirement.geometry::geography,
                                requirement.radius_metres)::geometry
                        WHEN {MasterDataCodes.SpatialRequirementTypes.RouteBuffer} THEN
                            ST_Buffer(requirement.geometry::geography,
                                requirement.radius_metres)::geometry
                        ELSE requirement.geometry
                    END AS target_area
                FROM commercial.brief_spatial_requirements requirement
                WHERE requirement.tenant_id = {tenantId.Value}
                  AND requirement.brief_version_id = {briefVersionId}
                  AND requirement.is_verified
            ), evaluated AS (
                SELECT candidate.*, requirement.id AS requirement_id,
                    requirement.priority_code,
                    CASE WHEN requirement.priority_code =
                            {MasterDataCodes.SpatialRequirementPriorities.Excluded}
                        THEN
                            (candidate.point_location IS NOT NULL AND
                                CASE WHEN requirement.requirement_type_code IN (
                                        {MasterDataCodes.SpatialRequirementTypes.PointRadius},
                                        {MasterDataCodes.SpatialRequirementTypes.RouteBuffer})
                                    THEN ST_DWithin(candidate.point_location,
                                        requirement.geometry::geography,
                                        requirement.radius_metres)
                                    ELSE ST_Covers(requirement.geometry,
                                        candidate.point_location::geometry) END)
                            OR (candidate.coverage_geometry IS NOT NULL AND
                                ST_Intersects(candidate.coverage_geometry,
                                    requirement.target_area))
                            OR (candidate.catchment_geometry IS NOT NULL AND
                                ST_Intersects(candidate.catchment_geometry,
                                    requirement.target_area))
                            OR (candidate.route_geometry IS NOT NULL AND
                                CASE WHEN requirement.requirement_type_code IN (
                                        {MasterDataCodes.SpatialRequirementTypes.PointRadius},
                                        {MasterDataCodes.SpatialRequirementTypes.RouteBuffer})
                                    THEN ST_DWithin(candidate.route_geometry::geography,
                                        requirement.geometry::geography,
                                        requirement.radius_metres)
                                    ELSE ST_Intersects(candidate.route_geometry,
                                        requirement.geometry) END)
                        ELSE CASE requirement.requirement_type_code
                        WHEN {MasterDataCodes.SpatialRequirementTypes.PointRadius} THEN
                            (candidate.point_location IS NOT NULL AND ST_DWithin(
                                candidate.point_location, requirement.geometry::geography,
                                requirement.radius_metres))
                            OR (candidate.route_geometry IS NOT NULL AND ST_DWithin(
                                candidate.route_geometry::geography,
                                requirement.geometry::geography,
                                requirement.radius_metres))
                            OR commercial.inventory_coverage_qualifies(
                                candidate.coverage_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                            OR commercial.inventory_coverage_qualifies(
                                candidate.catchment_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                        WHEN {MasterDataCodes.SpatialRequirementTypes.RouteBuffer} THEN
                            (candidate.point_location IS NOT NULL AND ST_DWithin(
                                candidate.point_location, requirement.geometry::geography,
                                requirement.radius_metres))
                            OR (candidate.route_geometry IS NOT NULL AND ST_DWithin(
                                candidate.route_geometry::geography,
                                requirement.geometry::geography,
                                requirement.radius_metres))
                            OR commercial.inventory_coverage_qualifies(
                                candidate.coverage_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                            OR commercial.inventory_coverage_qualifies(
                                candidate.catchment_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                        ELSE
                            (candidate.point_location IS NOT NULL AND
                                ST_Covers(requirement.geometry,
                                    candidate.point_location::geometry))
                            OR commercial.inventory_coverage_qualifies(
                                candidate.coverage_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                            OR commercial.inventory_coverage_qualifies(
                                candidate.catchment_geometry,
                                requirement.target_area,
                                requirement.coverage_threshold)
                            OR (candidate.route_geometry IS NOT NULL AND
                                ST_Intersects(candidate.route_geometry,
                                    requirement.geometry))
                        END
                    END AS is_match
                FROM candidate_geometry candidate
                LEFT JOIN requirements requirement ON TRUE
            )
            SELECT inventory_tenant_id AS "InventoryTenantId",
                listing_version_id AS "MarketplaceListingVersionId",
                product_version_id AS "ProductVersionId",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Required}), '[]')::text
                    AS "RequiredRequirementIdsJson",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Required}
                        AND is_match), '[]')::text AS "MatchedRequiredRequirementIdsJson",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Preferred}), '[]')::text
                    AS "PreferredRequirementIdsJson",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Preferred}
                        AND is_match), '[]')::text AS "MatchedPreferredRequirementIdsJson",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Excluded}), '[]')::text
                    AS "ExcludedRequirementIdsJson",
                COALESCE(jsonb_agg(requirement_id ORDER BY requirement_id)
                    FILTER (WHERE priority_code =
                        {MasterDataCodes.SpatialRequirementPriorities.Excluded}
                        AND is_match), '[]')::text AS "MatchedExcludedRequirementIdsJson"
            FROM evaluated
            GROUP BY inventory_tenant_id, listing_version_id, product_version_id
            """).ToListAsync(cancellationToken);
        return rows.ToDictionary(
            row => new PlanningInventoryKey(
                row.InventoryTenantId, row.MarketplaceListingVersionId,
                row.ProductVersionId),
            PlanningSpatialMatcher.ToView);
    }
}

internal static class PlanningSpatialMatcher
{
    private static readonly JsonSerializerOptions StoredJson =
        new(JsonSerializerDefaults.Web);

    internal static InventorySpatialMatchView ToView(PlanningSpatialMatchRow row)
    {
        var required = Read(row.RequiredRequirementIdsJson);
        var matchedRequired = Read(row.MatchedRequiredRequirementIdsJson);
        var preferred = Read(row.PreferredRequirementIdsJson);
        var matchedPreferred = Read(row.MatchedPreferredRequirementIdsJson);
        var excludedRequirements = Read(row.ExcludedRequirementIdsJson);
        var excluded = Read(row.MatchedExcludedRequirementIdsJson);
        var total = required.Length + preferred.Length;
        var score = total == 0
            ? 1m
            : decimal.Divide(matchedRequired.Length + matchedPreferred.Length, total);
        var gaps = new List<string>();
        if (required.Length > 0 && matchedRequired.Length == 0)
        {
            gaps.Add("NO_REQUIRED_SPATIAL_MATCH");
        }
        if (excluded.Length > 0)
        {
            gaps.Add("EXCLUDED_SPATIAL_MATCH");
        }
        return new(
            total > 0 || excludedRequirements.Length > 0,
            required, matchedRequired, preferred, matchedPreferred,
            excludedRequirements, excluded,
            excluded.Length > 0 ? 0m : score, gaps);
    }

    internal static EligibilityResult ApplyEligibility(
        EligibilityResult eligibility,
        InventorySpatialMatchView spatialMatch)
    {
        if (!eligibility.IsEligible || !spatialMatch.HasRequirements)
        {
            return eligibility;
        }
        if (spatialMatch.MatchedExcludedRequirementIds.Count > 0)
        {
            return Rejected("The product intersects an excluded Brief geography.");
        }
        if (spatialMatch.RequiredRequirementIds.Count > 0 &&
            spatialMatch.MatchedRequiredRequirementIds.Count == 0)
        {
            return Rejected("The product does not match a required Brief geography.");
        }
        return eligibility;
    }

    private static Guid[] Read(string json) =>
        JsonSerializer.Deserialize<Guid[]>(json, StoredJson) ?? [];

    private static EligibilityResult Rejected(string detail) => new(
        false, MasterDataCodes.RejectionReasons.IneligibleGeography, detail, null);
}

internal readonly record struct PlanningInventoryKey(
    Guid InventoryTenantId,
    Guid? MarketplaceListingVersionId,
    Guid ProductVersionId);
