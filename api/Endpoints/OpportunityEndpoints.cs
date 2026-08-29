using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Api.OpenApi;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityEndpoints
{
    public static IEndpointRouteBuilder MapOpportunityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Opportunity qualification")
            .RequireAuthorization();
        group.MapOpportunityQueries();
        group.MapOpportunityCommands();
        group.MapOpportunityWorkflowCommands();
        group.MapOpportunityTaskCommands();
        return endpoints;
    }

    internal static RouteHandlerBuilder WithGate4CommandProblems(
        this RouteHandlerBuilder builder,
        bool requiresVersion)
    {
        builder
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status400BadRequest,
                "application/problem+json")
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status401Unauthorized,
                "application/problem+json")
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status403Forbidden,
                "application/problem+json")
            .Produces<HumanSafeProblemDetails>(
                StatusCodes.Status409Conflict,
                "application/problem+json");
        if (requiresVersion)
        {
            builder.WithMetadata(new RequiresEntityVersionMetadata());
            builder.Produces<HumanSafeProblemDetails>(
                StatusCodes.Status428PreconditionRequired,
                "application/problem+json");
        }
        return builder;
    }

    internal static RouteHandlerBuilder WithGate4QueryProblems(
        this RouteHandlerBuilder builder) => builder
        .Produces<HumanSafeProblemDetails>(
            StatusCodes.Status400BadRequest,
            "application/problem+json")
        .Produces<HumanSafeProblemDetails>(
            StatusCodes.Status401Unauthorized,
            "application/problem+json")
        .Produces<HumanSafeProblemDetails>(
            StatusCodes.Status403Forbidden,
            "application/problem+json");
}
