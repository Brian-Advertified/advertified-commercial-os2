using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class CommercialPolicyEndpoints
{
    public static IEndpointRouteBuilder MapCommercialPolicyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/commercial-policy")
            .WithTags("Commercial settings")
            .RequireAuthorization();
        group.MapGet(string.Empty, GetCurrentAsync)
            .WithName("GetCurrentCommercialPolicy")
            .Produces<CommercialPolicyView?>()
            .WithQueryProblems();
        group.MapPut(string.Empty, SaveAsync)
            .WithName("SaveCommercialPolicy")
            .Produces<CommercialPolicyView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> GetCurrentAsync(
        Guid tenantId,
        HttpContext context,
        ICurrentIdentity identity,
        ICommercialPolicyReader reader,
        CancellationToken cancellationToken)
    {
        var result = await reader.GetCurrentAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken);
        if (result is not null)
        {
            CommandEnvelopeFactory.SetEntityHeaders(context, result.Version);
        }
        return result is null
            ? Results.Text("null", "application/json")
            : Results.Ok(result);
    }

    private static async Task<IResult> SaveAsync(
        Guid tenantId,
        SaveCommercialPolicyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        ICommercialPolicyCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await CommandEndpointExecutor.ExecuteResultAsync(
            tenantId,
            command,
            context,
            identity,
            timeProvider,
            requireVersion: true,
            commands.SaveAsync,
            cancellationToken,
            allowZeroVersion: true);
        return Results.Ok(result.Data);
    }
}
