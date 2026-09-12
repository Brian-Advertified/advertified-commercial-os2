using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Intelligence;
using Advertified.Commercial.Api.Authentication;

namespace Advertified.Commercial.Api.Endpoints;

public static class MarketIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapMarketIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/market",
                GetLatestAsync)
            .WithName("GetLatestMarketIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/market",
                AnalyseAsync)
            .WithName("RunMarketIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetLatestAsync(
        Guid tenantId,
        Guid briefVersionId,
        ICurrentIdentity identity,
        IMarketIntelligenceService service,
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
        IMarketIntelligenceService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.AnalyseBriefAsync(
            identity.ActorId.Value, tenantId, briefVersionId, cancellationToken));
}
