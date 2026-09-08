using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Api.Endpoints;

public static class PublicInventoryEndpoints
{
    public static IEndpointRouteBuilder MapPublicInventoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/public/inventory-summary", GetAsync)
            .WithName("GetPublicInventorySummary")
            .WithTags("Public inventory")
            .AllowAnonymous()
            .Produces<PublicInventorySummaryView>();
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        IPublicInventorySummaryReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetAsync(cancellationToken));
}
