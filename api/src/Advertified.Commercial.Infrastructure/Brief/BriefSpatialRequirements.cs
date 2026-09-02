using System.Text.Json;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Brief;

internal sealed record NormalizedBriefSpatialRequirement(
    Guid Id,
    string Type,
    string Priority,
    string Label,
    string RawGeoJson,
    bool ParseGeoJson,
    decimal? RadiusMetres,
    decimal? CoverageThreshold,
    bool BufferInferred,
    string? BoundarySource,
    string? BoundaryVersion,
    string SourceLocator,
    bool RequestedVerified);

internal static class BriefSpatialRequirements
{
    private const decimal DefaultRouteBufferMetres = 500m;
    private const decimal DefaultCoverageThreshold = 0.50m;

    internal static NormalizedBriefSpatialRequirement[] Normalize(
        IReadOnlyList<BriefSpatialRequirementInput>? values)
    {
        if (values is null) return [];
        if (values.Count > 100) throw new ArgumentOutOfRangeException(nameof(values));
        return values.Select(Normalize).ToArray();
    }

    internal static async Task InsertAsync(
        GovernanceDbContext dbContext,
        Guid tenantId,
        Guid briefVersionId,
        Guid actorId,
        DateTimeOffset now,
        IReadOnlyList<NormalizedBriefSpatialRequirement> values,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                WITH prepared AS (
                    SELECT CASE WHEN {value.ParseGeoJson}
                        THEN commercial.try_parse_geojson({value.RawGeoJson})
                        ELSE NULL END AS geometry)
                INSERT INTO commercial.brief_spatial_requirements (
                    id, tenant_id, brief_version_id, requirement_type_code,
                    priority_code, label, raw_geometry_text, geometry, radius_metres,
                    coverage_threshold, buffer_inferred, boundary_source,
                    boundary_version, source_locator, is_verified, created_by, created_at_utc)
                SELECT {value.Id}, {tenantId}, {briefVersionId}, {value.Type},
                    {value.Priority}, {value.Label}, {value.RawGeoJson}, geometry,
                    {value.RadiusMetres}, {value.CoverageThreshold},
                    {value.BufferInferred}, {value.BoundarySource}, {value.BoundaryVersion},
                    {value.SourceLocator},
                    {value.RequestedVerified} AND geometry IS NOT NULL
                        AND ST_IsValid(geometry),
                    {actorId}, {now}
                FROM prepared
                """, cancellationToken);
        }
    }

    internal static Task<int> CreateClarificationTaskAsync(
        GovernanceDbContext dbContext,
        Guid tenantId,
        Guid? opportunityId,
        Guid briefVersionId,
        long briefVersion,
        Guid assignee,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES ({Guid.NewGuid()}, {tenantId}, {opportunityId},
                {MasterDataCodes.HumanTaskTypes.SpatialClarification},
                {MasterDataCodes.LifecycleStatuses.Pending},
                {"Clarify campaign geography"},
                {"Verify the supplied point, boundary, catchment or route before spatial matching."},
                {MasterDataReferences.CommercialResourceTypes.BriefVersion.Value},
                {briefVersionId}, {briefVersion}, {assignee}, {"{}"}::jsonb, 1, {now})
            """, cancellationToken);

    internal static Task<bool> HasUnverifiedAsync(
        GovernanceDbContext dbContext,
        Guid tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.brief_spatial_requirements
                WHERE tenant_id = {tenantId} AND brief_version_id = {briefVersionId}
                  AND NOT is_verified) AS "Value"
            """).SingleAsync(cancellationToken);

    private static NormalizedBriefSpatialRequirement Normalize(
        BriefSpatialRequirementInput value)
    {
        var type = RequiredCode(value.Type, SpatialTypes);
        var priority = RequiredCode(value.Priority, SpatialPriorities);
        var label = OpportunityCommandSupport.Required(value.Label, 500, nameof(value.Label));
        var raw = OpportunityCommandSupport.Required(value.GeoJson, 100_000, nameof(value.GeoJson));
        var expectedGeometry = ExpectedGeometryTypes(type);
        var parsed = HasExpectedGeoJson(raw, expectedGeometry);
        var inferred = type == MasterDataCodes.SpatialRequirementTypes.RouteBuffer &&
            !value.RadiusMetres.HasValue;
        var radius = inferred ? DefaultRouteBufferMetres : value.RadiusMetres;
        var threshold = value.CoverageThreshold ?? DefaultCoverageThreshold;
        var source = OpportunityCommandSupport.Optional(
            value.BoundarySource, 300, nameof(value.BoundarySource));
        var version = OpportunityCommandSupport.Optional(
            value.BoundaryVersion, 100, nameof(value.BoundaryVersion));
        var validParameters = ValidParameters(type, radius, threshold, source, version);
        return new(
            Guid.NewGuid(), type, priority, label, raw, parsed && validParameters,
            radius, threshold, inferred, source, version,
            OpportunityCommandSupport.Required(
                value.SourceLocator ?? "brief:spatial-requirement", 1_000,
                nameof(value.SourceLocator)),
            value.IsVerified && parsed && validParameters);
    }

    private static string RequiredCode(string value, HashSet<string> allowed)
    {
        var code = value?.Trim().ToUpperInvariant();
        return code is not null && allowed.Contains(code)
            ? code
            : throw new ArgumentException("Select a supported spatial requirement value.");
    }

    private static bool ValidParameters(
        string type,
        decimal? radius,
        decimal? threshold,
        string? boundarySource,
        string? boundaryVersion) =>
        (type is not (MasterDataCodes.SpatialRequirementTypes.PointRadius or
                MasterDataCodes.SpatialRequirementTypes.RouteBuffer) || radius > 0) &&
        (threshold is null or > 0 and <= 1) &&
        (type != MasterDataCodes.SpatialRequirementTypes.AdminBoundary ||
            boundarySource is not null && boundaryVersion is not null);

    private static bool HasExpectedGeoJson(string raw, HashSet<string> expected)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("type", out var type) &&
                expected.Contains(type.GetString() ?? string.Empty) &&
                root.TryGetProperty("coordinates", out var coordinates) &&
                coordinates.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static HashSet<string> ExpectedGeometryTypes(string type) => type switch
    {
        MasterDataCodes.SpatialRequirementTypes.PointRadius =>
            new HashSet<string>(["Point"], StringComparer.Ordinal),
        MasterDataCodes.SpatialRequirementTypes.RouteBuffer =>
            new HashSet<string>(["LineString", "MultiLineString"], StringComparer.Ordinal),
        _ => new HashSet<string>(["Polygon", "MultiPolygon"], StringComparer.Ordinal),
    };

    private static readonly HashSet<string> SpatialTypes = new(StringComparer.Ordinal)
    {
        MasterDataCodes.SpatialRequirementTypes.PointRadius,
        MasterDataCodes.SpatialRequirementTypes.AdminBoundary,
        MasterDataCodes.SpatialRequirementTypes.Catchment,
        MasterDataCodes.SpatialRequirementTypes.RouteBuffer,
    };

    private static readonly HashSet<string> SpatialPriorities = new(StringComparer.Ordinal)
    {
        MasterDataCodes.SpatialRequirementPriorities.Required,
        MasterDataCodes.SpatialRequirementPriorities.Preferred,
        MasterDataCodes.SpatialRequirementPriorities.Excluded,
    };
}
