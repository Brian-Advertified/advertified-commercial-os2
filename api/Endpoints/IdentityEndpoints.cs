using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Api.Errors;

namespace Advertified.Commercial.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Identity")
            .RequireAuthorization();

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .Produces<CurrentUserView>()
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status401Unauthorized,
                "application/problem+json")
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status403Forbidden,
                "application/problem+json");

        group.MapGet("/workspaces", ListWorkspacesAsync)
            .WithName("ListWorkspaces")
            .Produces<IReadOnlyList<WorkspaceView>>()
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status401Unauthorized,
                "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        ICurrentIdentity identity,
        IIdentityWorkspaceReader reader,
        CancellationToken cancellationToken)
    {
        EnsureHuman(identity);
        var view = await reader.GetCurrentUserAsync(identity.UserId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<IReadOnlyList<WorkspaceView>> ListWorkspacesAsync(
        ICurrentIdentity identity,
        IIdentityWorkspaceReader reader,
        CancellationToken cancellationToken)
    {
        EnsureHuman(identity);
        return reader.ListWorkspacesAsync(identity.UserId, cancellationToken);
    }

    private static void EnsureHuman(ICurrentIdentity identity)
    {
        if (identity.IsServiceIdentity)
        {
            throw new UnauthorizedAccessException("Human identity required.");
        }
    }
}
