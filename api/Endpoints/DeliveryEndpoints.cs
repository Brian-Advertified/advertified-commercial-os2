using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class DeliveryEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var campaigns = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/campaigns")
            .WithTags("Campaign delivery").RequireAuthorization();
        campaigns.MapPost("/{campaignId:guid}/delivery-proofs", SubmitAsync)
            .WithName("SubmitDeliveryProof").Produces<DeliveryProofView>()
            .WithCommandProblems(requiresVersion: false);
        campaigns.MapPost(
                "/{campaignId:guid}/delivery-proofs/{proofId:guid}:review", ReviewAsync)
            .WithName("ReviewDeliveryProof").Produces<DeliveryProofView>()
            .WithCommandProblems(requiresVersion: true);

        var requests = endpoints.MapGroup(
                "/api/v1/tenants/{tenantId:guid}/delivery-proof-requests")
            .WithTags("Campaign delivery").RequireAuthorization();
        requests.MapGet(string.Empty, ListRequestsAsync)
            .WithName("ListDeliveryProofRequests")
            .Produces<IReadOnlyList<DeliveryProofRequestView>>().WithQueryProblems();

        var proofs = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/delivery-proofs")
            .WithTags("Campaign delivery").RequireAuthorization();
        proofs.MapGet("/{proofId:guid}", GetAsync)
            .WithName("GetDeliveryProof").Produces<DeliveryProofView>().WithQueryProblems();
        return endpoints;
    }

    private static async Task<IResult> ListRequestsAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IDeliveryProofReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListRequestsAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static Task<IResult> SubmitAsync(
        Guid tenantId, Guid campaignId, SubmitDeliveryProofCommand command,
        HttpContext context, ICurrentIdentity identity, IDeliveryProofCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, false,
            (envelope, token) => commands.SubmitAsync(campaignId, envelope, token),
            cancellationToken);

    private static Task<IResult> ReviewAsync(
        Guid tenantId, Guid campaignId, Guid proofId, ReviewDeliveryProofCommand command,
        HttpContext context, ICurrentIdentity identity, IDeliveryProofCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ReviewAsync(
                campaignId, proofId, envelope, token), cancellationToken);

    private static async Task<IResult> GetAsync(
        Guid tenantId, Guid proofId, HttpContext context, ICurrentIdentity identity,
        IDeliveryProofReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), proofId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }
}
