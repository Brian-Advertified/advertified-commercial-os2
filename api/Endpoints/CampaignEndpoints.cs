using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/campaigns")
            .WithTags("Campaign delivery").RequireAuthorization();
        group.MapGet(string.Empty, ListAsync)
            .WithName("ListCampaigns")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces<IReadOnlyList<CampaignView>>()
            .WithQueryProblems();
        group.MapGet("/{campaignId:guid}", GetAsync)
            .WithName("GetCampaign")
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .Produces<CampaignView>().WithQueryProblems();
        group.MapPost("/{campaignId:guid}:confirm-bookings", ConfirmBookingsAsync)
            .WithName("ConfirmCampaignBookings").Produces<CampaignView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/{campaignId:guid}:start", StartAsync)
            .WithName("StartCampaign").Produces<CampaignView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/{campaignId:guid}:complete", CompleteAsync)
            .WithName("CompleteCampaign").Produces<CampaignView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        ICampaignReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid tenantId,
        Guid campaignId,
        HttpContext context,
        ICurrentIdentity identity,
        ICampaignReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), campaignId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<IResult> ConfirmBookingsAsync(
        Guid tenantId,
        Guid campaignId,
        ConfirmCampaignBookingsCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        ICampaignCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ConfirmBookingsAsync(
                campaignId, envelope, token), cancellationToken);

    private static Task<IResult> StartAsync(
        Guid tenantId,
        Guid campaignId,
        StartCampaignCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        ICampaignCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, campaignId, command, context, identity, commands.StartAsync,
            clock, cancellationToken);

    private static Task<IResult> CompleteAsync(
        Guid tenantId,
        Guid campaignId,
        CompleteCampaignCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        ICampaignCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        ExecuteAsync(tenantId, campaignId, command, context, identity, commands.CompleteAsync,
            clock, cancellationToken);

    private static Task<IResult> ExecuteAsync<TCommand>(
        Guid tenantId,
        Guid campaignId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        Func<Guid, CommandEnvelope<TCommand>, CancellationToken,
            Task<CommandResult<CampaignView>>> execute,
        TimeProvider clock,
        CancellationToken cancellationToken)
        where TCommand : notnull =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => execute(campaignId, envelope, token), cancellationToken);
}
