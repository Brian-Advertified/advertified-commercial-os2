using Advertified.Commercial.Application.AgentOperations;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class AgentOperationsEndpoints
{
    public static IEndpointRouteBuilder MapAgentOperationsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/agent-operations",
                GetAgentOperationsAsync)
            .WithName("GetAgentOperations")
            .WithTags("Agent operations")
            .Produces<AgentOperationsView>()
            .WithQueryProblems()
            .RequireAuthorization();
        endpoints.MapGet(
                "/api/v1/tenants/{tenantId:guid}/agent-operations/{runId:guid}",
                GetAgentOperationProgressAsync)
            .WithName("GetAgentOperationProgress")
            .WithTags("Agent operations")
            .Produces<AgentOperationProgressView>()
            .WithQueryProblems()
            .RequireAuthorization();
        return endpoints;
    }

    private static Task<AgentOperationsView> GetAgentOperationsAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IAgentOperationsReader reader,
        CancellationToken cancellationToken) => reader.GetAsync(
            identity.ActorId,
            new TenantId(tenantId),
            cancellationToken);

    private static Task<AgentOperationProgressView> GetAgentOperationProgressAsync(
        Guid tenantId,
        Guid runId,
        ICurrentIdentity identity,
        IAgentOperationsReader reader,
        CancellationToken cancellationToken) => reader.GetProgressAsync(
            identity.ActorId,
            new TenantId(tenantId),
            runId,
            cancellationToken);
}
