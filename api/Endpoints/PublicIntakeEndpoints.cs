using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Onboarding;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class PublicIntakeEndpoints
{
    public static IEndpointRouteBuilder MapPublicIntakeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/public-intake", SubmitAsync)
            .WithName("SubmitPublicIntake")
            .WithTags("Public intake")
            .AllowAnonymous()
            .RequireRateLimiting(RequestRateLimitPolicies.PublicIntake)
            .Produces<PublicIntakeView>(StatusCodes.Status202Accepted);

        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/public-intake")
            .WithTags("Public intake")
            .RequireAuthorization();
        group.MapGet("", ListAsync)
            .WithName("ListPublicIntake")
            .Produces<Advertified.Commercial.Application.Foundation.CursorPage<PublicIntakeView>>()
            .WithQueryProblems();
        group.MapPost("/{requestId:guid}:provision", ProvisionAsync)
            .WithName("ProvisionPublicIntake")
            .Produces<PublicIntakeView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/{requestId:guid}:reject", RejectAsync)
            .WithName("RejectPublicIntake")
            .Produces<PublicIntakeView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/{requestId:guid}:resolve", ResolveAsync)
            .WithName("ResolvePublicIntake")
            .Produces<PublicIntakeView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> SubmitAsync(
        SubmitPublicIntakeRequest request,
        HttpContext context,
        IPublicIntakeCommands commands,
        CancellationToken cancellationToken)
    {
        var view = await commands.SubmitAsync(request, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Accepted(value: view);
    }

    private static Task<Advertified.Commercial.Application.Foundation.CursorPage<PublicIntakeView>> ListAsync(
        Guid tenantId,
        string? status,
        int limit,
        string? cursor,
        ICurrentIdentity identity,
        IPublicIntakeReader reader,
        CancellationToken cancellationToken) =>
        reader.ListAsync(
            identity.ActorId,
            new TenantId(tenantId),
            status,
            limit,
            cursor,
            cancellationToken);

    private static Task<IResult> ProvisionAsync(
        Guid tenantId,
        Guid requestId,
        ProvisionPublicIntakeCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IPublicIntakeCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            requireVersion: true,
            (envelope, token) => commands.ProvisionAsync(requestId, envelope, token),
            cancellationToken);

    private static Task<IResult> RejectAsync(
        Guid tenantId,
        Guid requestId,
        RejectPublicIntakeCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IPublicIntakeCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            requireVersion: true,
            (envelope, token) => commands.RejectAsync(requestId, envelope, token),
            cancellationToken);

    private static Task<IResult> ResolveAsync(
        Guid tenantId,
        Guid requestId,
        ResolvePublicIntakeCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IPublicIntakeCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            requireVersion: true,
            (envelope, token) => commands.ResolveAsync(requestId, envelope, token),
            cancellationToken);
}
