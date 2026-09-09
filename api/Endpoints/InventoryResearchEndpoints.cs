using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class InventoryResearchEndpoints
{
    public static IEndpointRouteBuilder MapInventoryResearchEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/inventory-research")
            .WithTags("Inventory research enrichment").RequireAuthorization();
        group.MapPost(string.Empty, RegisterAsync)
            .WithName("RegisterInventoryResearchDataset")
            .Produces<InventoryResearchDatasetView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapGet("/{datasetId:guid}", GetAsync)
            .WithName("GetInventoryResearchDataset")
            .Produces<InventoryResearchDatasetView>().WithQueryProblems();
        group.MapPost("/matches/{matchId:guid}:review", ReviewAsync)
            .WithName("ReviewInventoryResearchMatch")
            .Produces<InventoryResearchDatasetView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        Guid tenantId, RegisterInventoryResearchDatasetCommand command,
        HttpContext context, ICurrentIdentity identity,
        IInventoryResearchCommands commands, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await CommandEndpointExecutor.ExecuteResultAsync(
            tenantId, command, context, identity, clock, requireVersion: false,
            commands.RegisterAsync, cancellationToken);
        return Results.Created(
            $"/api/v1/tenants/{tenantId}/inventory-research/{result.Data.Id}", result.Data);
    }

    private static Task<InventoryResearchDatasetView> GetAsync(
        Guid tenantId, Guid datasetId, ICurrentIdentity identity,
        IInventoryResearchReader reader, CancellationToken cancellationToken) =>
        reader.GetAsync(identity.ActorId, new TenantId(tenantId), datasetId, cancellationToken);

    private static async Task<IResult> ReviewAsync(
        Guid tenantId, Guid matchId, ReviewInventoryResearchMatchCommand command,
        HttpContext context, ICurrentIdentity identity,
        IInventoryResearchCommands commands, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await CommandEndpointExecutor.ExecuteResultAsync(
            tenantId, command, context, identity, clock, requireVersion: true,
            (envelope, token) => commands.ReviewAsync(matchId, envelope, token),
            cancellationToken);
        return Results.Ok(result.Data);
    }
}
