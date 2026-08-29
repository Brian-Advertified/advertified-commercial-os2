using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityQueryEndpoints
{
    public static RouteGroupBuilder MapOpportunityQueries(this RouteGroupBuilder group)
    {
        group.MapGet("/opportunities", ListOpportunitiesAsync)
            .WithName("ListOpportunities")
            .Produces<CursorPage<OpportunityView>>()
            .WithQueryProblems();
        group.MapGet("/opportunities/{opportunityId:guid}", GetOpportunityAsync)
            .WithName("GetOpportunity")
            .Produces<OpportunityDetailView>()
            .WithQueryProblems();
        group.MapGet("/strategies/{strategyId:guid}", GetStrategyAsync)
            .WithName("GetStrategy")
            .Produces<StrategyVersionView>()
            .WithQueryProblems();
        group.MapGet("/agent-runs/{runId:guid}", GetRunAsync)
            .WithName("GetAgentRun")
            .Produces<AgentRunView>()
            .WithQueryProblems();
        group.MapGet("/human-tasks", ListTasksAsync)
            .WithName("ListHumanTasks")
            .Produces<CursorPage<HumanTaskView>>()
            .WithQueryProblems();
        return group;
    }

    private static Task<CursorPage<OpportunityView>> ListOpportunitiesAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        CancellationToken cancellationToken) => reader.ListAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);

    private static async Task<IResult> GetOpportunityAsync(
        Guid tenantId,
        Guid opportunityId,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), opportunityId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Opportunity.Version);
        return Results.Ok(view);
    }

    private static async Task<IResult> GetStrategyAsync(
        Guid tenantId,
        Guid strategyId,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetStrategyAsync(
            identity.ActorId, new TenantId(tenantId), strategyId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static async Task<IResult> GetRunAsync(
        Guid tenantId,
        Guid runId,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetRunAsync(
            identity.ActorId, new TenantId(tenantId), runId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<CursorPage<HumanTaskView>> ListTasksAsync(
        Guid tenantId,
        int? limit,
        string? cursor,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        CancellationToken cancellationToken) => reader.ListTasksAsync(
            identity.ActorId,
            new TenantId(tenantId),
            limit ?? 0,
            cursor,
            cancellationToken);
}
