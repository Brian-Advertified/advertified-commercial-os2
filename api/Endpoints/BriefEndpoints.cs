using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class BriefEndpoints
{
    public static IEndpointRouteBuilder MapBriefEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Canonical Brief")
            .RequireAuthorization();
        group.MapPost("/briefs:understand", UnderstandBriefAsync)
            .WithName("UnderstandSuppliedBrief")
            .Produces<SuppliedBriefUnderstandingView>()
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithQueryProblems();
        group.MapGet("/briefs", ListBriefsAsync)
            .WithName("ListCampaignBriefs")
            .Produces<IReadOnlyList<CampaignBriefSummaryView>>()
            .WithQueryProblems();
        group.MapPost("/briefs", CreateBriefAsync)
            .WithName("CreateCampaignBrief")
            .Produces<CampaignBriefSummaryView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/briefs/{briefId:guid}/versions", CreateVersionAsync)
            .WithName("CreateBriefVersion")
            .Produces<BriefVersionView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapGet("/briefs/{briefId:guid}", GetBriefAsync)
            .WithName("GetCampaignBrief")
            .Produces<CampaignBriefView>()
            .WithQueryProblems();
        group.MapPost("/brief-versions/{versionId:guid}:submit", SubmitAsync)
            .WithName("SubmitBriefVersion")
            .Produces<BriefVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{versionId:guid}:ready", MarkReadyAsync)
            .WithName("MarkBriefVersionReady")
            .Produces<BriefVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{versionId:guid}:approve", ApproveAsync)
            .WithName("ApproveBriefVersion")
            .Produces<BriefVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{versionId:guid}:reject", RejectAsync)
            .WithName("RejectBriefVersion")
            .Produces<BriefVersionView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> ListBriefsAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IBriefReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.ListAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> UnderstandBriefAsync(
        Guid tenantId,
        UnderstandSuppliedBriefRequest request,
        ICurrentIdentity identity,
        ISuppliedBriefUnderstandingService service,
        CancellationToken cancellationToken) => Results.Ok(await service.UnderstandAsync(
            identity.ActorId,
            new TenantId(tenantId),
            request,
            cancellationToken));

    private static Task<IResult> CreateBriefAsync(
        Guid tenantId,
        CreateBriefCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, false, commands.CreateAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/briefs/{result.Data.Id}", result.Data),
            cancellationToken);

    private static Task<IResult> CreateVersionAsync(
        Guid tenantId,
        Guid briefId,
        CreateBriefVersionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, false,
            (envelope, token) => commands.CreateVersionAsync(briefId, envelope, token),
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/briefs/{briefId}", result.Data),
            cancellationToken);

    private static async Task<IResult> GetBriefAsync(
        Guid tenantId,
        Guid briefId,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), briefId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Brief.Version);
        return Results.Ok(view);
    }

    private static Task<IResult> SubmitAsync(
        Guid tenantId,
        Guid versionId,
        SubmitBriefVersionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, versionId, command, context, identity, commands.SubmitAsync,
            clock, cancellationToken);

    private static Task<IResult> MarkReadyAsync(
        Guid tenantId,
        Guid versionId,
        MarkBriefVersionReadyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, versionId, command, context, identity, commands.MarkReadyAsync,
            clock, cancellationToken);

    private static Task<IResult> ApproveAsync(
        Guid tenantId,
        Guid versionId,
        ApproveBriefVersionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, versionId, command, context, identity, commands.ApproveAsync,
            clock, cancellationToken);

    private static Task<IResult> RejectAsync(
        Guid tenantId,
        Guid versionId,
        RejectBriefVersionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBriefCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, versionId, command, context, identity, commands.RejectAsync,
            clock, cancellationToken);

    private static Task<IResult> ExecuteVersionedAsync<TCommand>(
        Guid tenantId,
        Guid versionId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        Func<Guid, CommandEnvelope<TCommand>, CancellationToken,
            Task<CommandResult<BriefVersionView>>> execute,
        TimeProvider clock,
        CancellationToken cancellationToken)
        where TCommand : notnull => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => execute(versionId, envelope, token),
            result => Results.Ok(result.Data), cancellationToken);

    private static Task<IResult> ExecuteAsync<TCommand, TResult>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider clock,
        bool requiresVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        Func<CommandResult<TResult>, IResult> response,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, clock, requiresVersion,
            execute, response, cancellationToken);
}
