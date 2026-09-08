using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Api.Authentication;

namespace Advertified.Commercial.Api.Endpoints;

public static class PlaceDiscoveryEndpoints
{
    public static IEndpointRouteBuilder MapPlaceDiscoveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/tenants/{tenantId:guid}/inventory-places", SearchAsync)
            .WithTags("Inventory truth").RequireAuthorization()
            .WithName("SearchInventoryPlaces").Produces<IReadOnlyList<InventoryPlaceView>>()
            .WithQueryProblems();
        endpoints.MapGet("/api/v1/tenants/{tenantId:guid}/place-discovery", DiscoverAsync)
            .WithTags("Inventory truth").RequireAuthorization()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .WithName("DiscoverNamedPlaces").Produces<PlaceDiscoveryResult>().WithQueryProblems();
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(Guid tenantId, string search,
        ICurrentIdentity identity, IInventoryReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.SearchPlacesAsync(identity.ActorId, new TenantId(tenantId),
            search, cancellationToken));

    private static async Task<IResult> DiscoverAsync(Guid tenantId, string search,
        ICurrentIdentity identity, ITenantAuthorizer authorizer, IPlaceDiscoveryProvider provider,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(identity.ActorId, new TenantId(tenantId),
            MasterDataReferences.Permissions.InventoryView, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Place search access denied.");
        return Results.Ok(await provider.SearchAsync(search, cancellationToken));
    }
}
