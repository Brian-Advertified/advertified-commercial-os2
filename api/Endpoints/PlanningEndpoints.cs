using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Planning;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class PlanningEndpoints
{
    public static IEndpointRouteBuilder MapPlanningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Canonical Planning")
            .RequireAuthorization();
        group.MapGet("/brief-versions/{briefVersionId:guid}/planning", GetWorkspaceAsync)
            .WithName("GetPlanningWorkspace").Produces<PlanningWorkspaceView>()
            .WithQueryProblems();
        group.MapPost("/brief-versions/{briefVersionId:guid}/campaign-mode:select",
                SelectCampaignModeAsync)
            .WithName("SelectCampaignMode").Produces<CampaignModeSelectionView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/brief-versions/{briefVersionId:guid}/audiences:generate",
                GenerateAudiencesAsync)
            .WithName("GenerateAudiences").Produces<AudienceDefinitionSetView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/audience-definition-sets/{audienceSetId:guid}:approve",
                ApproveAudienceStrategyAsync)
            .WithName("ApproveAudienceStrategy").Produces<AudienceDefinitionSetView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{briefVersionId:guid}/media-mixes:generate",
                GenerateMixAsync)
            .WithName("GenerateMediaMix").Produces<MediaMixVersionView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/media-mix-versions/{mixVersionId:guid}:update", UpdateMixAsync)
            .WithName("UpdateMediaMix").Produces<MediaMixVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/media-mix-versions/{mixVersionId:guid}:approve", ApproveMixAsync)
            .WithName("ApproveMediaMix").Produces<MediaMixVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{briefVersionId:guid}/shortlists:generate",
                GenerateShortlistAsync)
            .WithName("GenerateInventoryShortlist")
            .Produces<InventoryShortlistVersionView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/shortlist-versions/{shortlistVersionId:guid}:select", SelectShortlistAsync)
            .WithName("SelectInventoryShortlist")
            .Produces<InventoryShortlistVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/brief-versions/{briefVersionId:guid}/media-plans:generate",
                GeneratePlanAsync)
            .WithName("GenerateMediaPlan").Produces<MediaPlanVersionView>()
            .WithCommandProblems(requiresVersion: false);
        group.MapGet("/media-plans/{planVersionId:guid}", GetPlanAsync)
            .WithName("GetMediaPlan").Produces<MediaPlanVersionView>()
            .WithQueryProblems();
        group.MapPost(
                "/media-plan-versions/{planVersionId:guid}/objections/{objectionCode}:resolve",
                ResolveObjectionAsync)
            .WithName("ResolveMediaPlanObjection").Produces<MediaPlanVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/media-plan-versions/{planVersionId:guid}:approve", ApprovePlanAsync)
            .WithName("ApproveMediaPlan").Produces<MediaPlanVersionView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> GetWorkspaceAsync(
        Guid tenantId,
        Guid briefVersionId,
        HttpContext context,
        ICurrentIdentity identity,
        IPlanningReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetWorkspaceAsync(
            identity.ActorId, new TenantId(tenantId), briefVersionId, cancellationToken);
        var version = view.MediaPlan?.Version ?? view.Shortlist?.Version ??
            view.MediaMix?.Version ?? 1;
        CommandEnvelopeFactory.SetEntityHeaders(context, version);
        return Results.Ok(view);
    }

    private static async Task<IResult> GetPlanAsync(
        Guid tenantId,
        Guid planVersionId,
        HttpContext context,
        ICurrentIdentity identity,
        IPlanningReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetPlanAsync(
            identity.ActorId, new TenantId(tenantId), planVersionId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<IResult> SelectCampaignModeAsync(
        Guid tenantId, Guid briefVersionId, SelectCampaignModeCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteCreationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.SelectCampaignModeAsync(
                briefVersionId, envelope, token), cancellationToken);

    private static Task<IResult> GenerateAudiencesAsync(
        Guid tenantId, Guid briefVersionId, GenerateAudiencesCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteCreationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.GenerateAudiencesAsync(
                briefVersionId, envelope, token), cancellationToken);

    private static Task<IResult> ApproveAudienceStrategyAsync(
        Guid tenantId, Guid audienceSetId, ApproveAudienceStrategyCommand command,
        HttpContext context, ICurrentIdentity identity, IAudienceStrategyCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.ApproveAsync(
                audienceSetId, envelope, token), cancellationToken);

    private static Task<IResult> GenerateMixAsync(
        Guid tenantId, Guid briefVersionId, GenerateMediaMixCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteCreationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.GenerateMediaMixAsync(
                briefVersionId, envelope, token), cancellationToken);

    private static Task<IResult> UpdateMixAsync(
        Guid tenantId, Guid mixVersionId, UpdateMediaMixCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.UpdateMediaMixAsync(
                mixVersionId, envelope, token), cancellationToken);

    private static Task<IResult> ApproveMixAsync(
        Guid tenantId, Guid mixVersionId, ApproveMediaMixCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.ApproveMediaMixAsync(
                mixVersionId, envelope, token), cancellationToken);

    private static Task<IResult> GenerateShortlistAsync(
        Guid tenantId, Guid briefVersionId, GenerateShortlistCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteCreationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.GenerateShortlistAsync(
                briefVersionId, envelope, token), cancellationToken);

    private static Task<IResult> SelectShortlistAsync(
        Guid tenantId, Guid shortlistVersionId, SelectShortlistCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.SelectShortlistAsync(
                shortlistVersionId, envelope, token), cancellationToken);

    private static Task<IResult> GeneratePlanAsync(
        Guid tenantId, Guid briefVersionId, GenerateMediaPlanCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteCreationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.GenerateMediaPlanAsync(
                briefVersionId, envelope, token), cancellationToken);

    private static Task<IResult> ResolveObjectionAsync(
        Guid tenantId, Guid planVersionId, string objectionCode,
        ResolvePlanObjectionCommand command, HttpContext context,
        ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.ResolvePlanObjectionAsync(
                planVersionId, objectionCode, envelope, token), cancellationToken);

    private static Task<IResult> ApprovePlanAsync(
        Guid tenantId, Guid planVersionId, ApproveMediaPlanCommand command,
        HttpContext context, ICurrentIdentity identity, IPlanningCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteMutationAsync(
            tenantId, command, context, identity, clock,
            (envelope, token) => commands.ApproveMediaPlanAsync(
                planVersionId, envelope, token), cancellationToken);

    private static Task<IResult> ExecuteCreationAsync<TCommand, TView>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider clock,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TView : notnull => ExecutePlanningAsync(
            tenantId, command, context, identity, clock, false, execute, cancellationToken);

    private static Task<IResult> ExecuteMutationAsync<TCommand, TView>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider clock,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TView : notnull => ExecutePlanningAsync(
            tenantId, command, context, identity, clock, true, execute, cancellationToken);

    private static Task<IResult> ExecutePlanningAsync<TCommand, TView>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider clock,
        bool requiresVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TView : notnull => OpportunityCommandEndpoints.ExecuteAsync(
            tenantId, command, context, identity, clock, requiresVersion, execute,
            result => Results.Ok(result.Data), cancellationToken);
}
