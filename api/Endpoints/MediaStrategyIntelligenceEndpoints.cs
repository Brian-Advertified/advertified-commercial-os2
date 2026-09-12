using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Intelligence;

namespace Advertified.Commercial.Api.Endpoints;

public static class MediaStrategyIntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapMediaStrategyIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/media-strategy",
                GetLatestAsync)
            .WithName("GetLatestMediaStrategyIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/media-strategy",
                AnalyseAsync)
            .WithName("RunMediaStrategyIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/v1/tenants/{tenantId:guid}/brief-versions/{briefVersionId:guid}/intelligence/media-strategy/{artifactId:guid}/approve",
                ApproveAsync)
            .WithName("ApproveMediaStrategyIntelligence")
            .WithTags("Commercial intelligence")
            .Produces<IntelligenceArtifactView>()
            .WithQueryProblems()
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> GetLatestAsync(
        Guid tenantId,
        Guid briefVersionId,
        ICurrentIdentity identity,
        IMediaStrategyIntelligenceService service,
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
        IMediaStrategyIntelligenceService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.AnalyseBriefAsync(
            identity.ActorId.Value, tenantId, briefVersionId, cancellationToken));

    private static async Task<IResult> ApproveAsync(
        Guid tenantId,
        Guid briefVersionId,
        Guid artifactId,
        ApproveMediaStrategyRequest request,
        ICurrentIdentity identity,
        IMediaStrategyIntelligenceService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ApproveAsync(
            identity.ActorId.Value,
            tenantId,
            briefVersionId,
            artifactId,
            request.ExpectedVersion,
            cancellationToken));

    public sealed record ApproveMediaStrategyRequest(long ExpectedVersion);
}
