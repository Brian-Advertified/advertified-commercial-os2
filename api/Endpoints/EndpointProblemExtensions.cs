using Advertified.Commercial.Api.Errors;
using Advertified.Commercial.Api.OpenApi;

namespace Advertified.Commercial.Api.Endpoints;

internal static class EndpointProblemExtensions
{
    internal static RouteHandlerBuilder WithCommandProblems(
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

        if (!requiresVersion)
        {
            return builder;
        }

        builder.WithMetadata(new RequiresEntityVersionMetadata());
        return builder.Produces<HumanSafeProblemDetails>(
            StatusCodes.Status428PreconditionRequired,
            "application/problem+json");
    }

    internal static RouteHandlerBuilder WithQueryProblems(
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
