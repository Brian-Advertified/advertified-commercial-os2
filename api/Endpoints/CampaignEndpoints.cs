using Advertified.Commercial.Application.Campaign;
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
            .WithName("ListCampaigns").Produces<IReadOnlyList<CampaignView>>()
            .WithQueryProblems();
        group.MapGet("/{campaignId:guid}", GetAsync)
            .WithName("GetCampaign").Produces<CampaignView>().WithQueryProblems();
        group.MapPost("/{campaignId:guid}:confirm-bookings", ConfirmBookingsAsync)
            .WithName("ConfirmCampaignBookings").Produces<CampaignView>()
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
}
