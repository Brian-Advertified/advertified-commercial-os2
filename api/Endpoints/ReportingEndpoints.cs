using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Reporting;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/tenants/{tenantId:guid}/reporting/inventory-decisions", GetDecisionsAsync)
            .WithName("GetInventoryDecisionReport").WithTags("Operational and commercial reporting")
            .RequireAuthorization().Produces<InventoryDecisionReportView>().WithQueryProblems();
        endpoints.MapGet("/api/v1/tenants/{tenantId:guid}/reporting/operations", GetAsync)
            .WithName("GetOperationalReporting")
            .WithTags("Operational and commercial reporting")
            .RequireAuthorization()
            .Produces<OperationalReportingView>()
            .WithQueryProblems();
        return endpoints;
    }

    private static async Task<IResult> GetDecisionsAsync(
        Guid tenantId, Guid? briefVersionId, Guid? inventoryProductId, string? cursor,
        ICurrentIdentity identity, IInventoryDecisionReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.ReadAsync(
            identity.ActorId, new TenantId(tenantId), briefVersionId,
            inventoryProductId, cursor, cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid tenantId,
        DateOnly? from,
        DateOnly? to,
        Guid? clientAccountId,
        Guid? campaignId,
        string? channel,
        Guid? supplierId,
        string? status,
        Guid? ownerUserId,
        Guid? reviewerUserId,
        ICurrentIdentity identity,
        IOperationalReportingReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetAsync(
            identity.ActorId,
            new TenantId(tenantId),
            new ReportingQuery(from, to, clientAccountId, campaignId, channel,
                supplierId, status, ownerUserId, reviewerUserId),
            cancellationToken));
}
