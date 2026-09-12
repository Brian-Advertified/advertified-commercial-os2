using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Application.LocationIntelligence;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Api.Endpoints;

public static class LocationIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapLocationIntelligenceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/location-anchors",
                ResolveAnchorsAsync)
            .WithName("ResolveLocationAnchors")
            .WithTags("Commercial intelligence")
            .Produces<IReadOnlyList<DiscoveredLocation>>()
            .WithQueryProblems()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .RequireAuthorization();

        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/location",
                GetLatestAsync)
            .WithName("GetLatestLocationIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/location",
                AnalyseAsync)
            .WithName("RunLocationIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ResolveAnchorsAsync(
        Guid tenantId,
        string search,
        ICurrentIdentity identity,
        ITenantAuthorizer authorizer,
        ILocationDiscoveryProvider provider,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            identity.ActorId,
            new TenantId(tenantId),
            MasterDataReferences.Permissions.PlanGenerate,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Location research access denied.");
        return Results.Ok(await provider.SearchAsync(search, cancellationToken));
    }

    private static async Task<IResult> GetLatestAsync(
        Guid tenantId,
        Guid briefVersionId,
        ICurrentIdentity identity,
        ILocationIntelligenceService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLatestAsync(
            identity.ActorId.Value, tenantId, briefVersionId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> AnalyseAsync(
        Guid tenantId,
        Guid briefVersionId,
        ICurrentIdentity identity,
        ILocationIntelligenceService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.AnalyseBriefAsync(
            identity.ActorId.Value, tenantId, briefVersionId, cancellationToken));
}
