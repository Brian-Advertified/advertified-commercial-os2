namespace Advertified.Commercial.Api.Endpoints;

public static class FoundationEndpoints
{
    public static IEndpointRouteBuilder MapFoundationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Commercial foundation")
            .RequireAuthorization();
        group.MapFoundationQueries();
        group.MapFoundationCommands();
        return endpoints;
    }
}
