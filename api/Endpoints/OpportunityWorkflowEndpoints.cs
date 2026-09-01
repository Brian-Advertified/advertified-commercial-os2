using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityWorkflowEndpoints
{
    public static RouteGroupBuilder MapOpportunityWorkflowCommands(this RouteGroupBuilder group)
    {
        MapRunGeneration(group);
        MapHumanDecisions(group);
        MapRunManagement(group);
        return group;
    }

    private static void MapRunGeneration(RouteGroupBuilder group)
    {
        group.MapPost("/opportunities/{opportunityId:guid}/interpret", QueueInterpretationAsync)
            .WithName("QueueBusinessInterpretation")
            .Produces<AgentRunView>(StatusCodes.Status202Accepted)
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/opportunities/{opportunityId:guid}/angles:generate", QueueAnglesAsync)
            .WithName("QueueOpportunityAngles")
            .Produces<AgentRunView>(StatusCodes.Status202Accepted)
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/opportunities/{opportunityId:guid}/strategies:generate", QueueStrategyAsync)
            .WithName("QueueOpportunityStrategy")
            .Produces<AgentRunView>(StatusCodes.Status202Accepted)
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/opportunities/{opportunityId:guid}/briefs:draft", QueueBriefAsync)
            .WithName("QueueOpportunityBrief")
            .Produces<AgentRunView>(StatusCodes.Status202Accepted)
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: false);
    }

    private static void MapHumanDecisions(RouteGroupBuilder group)
    {
        group.MapPost("/business-interpretations/{interpretationId:guid}:confirm", ConfirmAsync)
            .WithName("ConfirmBusinessInterpretation")
            .Produces<BusinessInterpretationView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/opportunity-angles/{angleId:guid}:select", SelectAngleAsync)
            .WithName("SelectOpportunityAngle")
            .Produces<OpportunityAngleView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/critic-objections/{objectionId:guid}:resolve", ResolveObjectionAsync)
            .WithName("ResolveCriticObjection")
            .Produces<CriticObjectionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/strategy-versions/{strategyId:guid}:submit", SubmitStrategyAsync)
            .WithName("SubmitStrategyVersion")
            .Produces<StrategyVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/strategy-versions/{strategyId:guid}:approve", ApproveStrategyAsync)
            .WithName("ApproveStrategyVersion")
            .Produces<StrategyVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/strategy-versions/{strategyId:guid}:reject", RejectStrategyAsync)
            .WithName("RejectStrategyVersion")
            .Produces<StrategyVersionView>()
            .WithCommandProblems(requiresVersion: true);
    }

    private static void MapRunManagement(RouteGroupBuilder group)
    {
        group.MapPost("/agent-runs/{runId:guid}:resume", ResumeRunAsync)
            .WithName("ResumeAgentRun")
            .Produces<AgentRunView>()
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/agent-runs/{runId:guid}:cancel", CancelRunAsync)
            .WithName("CancelAgentRun")
            .Produces<AgentRunView>()
            .WithCommandProblems(requiresVersion: true);
    }

    private static Task<IResult> QueueInterpretationAsync(
        Guid tenantId,
        Guid opportunityId,
        QueueAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => QueueAsync(
            tenantId, opportunityId, MasterDataCodes.AgentRunKinds.Interpretation, command, context,
            identity, commands, timeProvider, cancellationToken);

    private static Task<IResult> QueueAnglesAsync(
        Guid tenantId,
        Guid opportunityId,
        QueueAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => QueueAsync(
            tenantId, opportunityId, MasterDataCodes.AgentRunKinds.Angles, command, context,
            identity, commands, timeProvider, cancellationToken);

    private static Task<IResult> QueueStrategyAsync(
        Guid tenantId,
        Guid opportunityId,
        QueueAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => QueueAsync(
            tenantId, opportunityId, MasterDataCodes.AgentRunKinds.StrategyCritic, command, context,
            identity, commands, timeProvider, cancellationToken);

    private static Task<IResult> QueueBriefAsync(
        Guid tenantId,
        Guid opportunityId,
        QueueAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => QueueAsync(
            tenantId, opportunityId, MasterDataCodes.AgentRunKinds.Brief, command, context,
            identity, commands, timeProvider, cancellationToken);

    private static Task<IResult> QueueAsync(
        Guid tenantId,
        Guid opportunityId,
        string runKind,
        QueueAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, false,
            (envelope, token) => commands.QueueRunAsync(
                opportunityId, runKind, envelope, token),
            result => Results.Accepted(
                $"/api/v1/tenants/{tenantId}/agent-runs/{result.Data.Id}", result.Data),
            cancellationToken);

    private static Task<IResult> ConfirmAsync(
        Guid tenantId,
        Guid interpretationId,
        ConfirmInterpretationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, interpretationId, command, context, identity, timeProvider,
            commands.ConfirmInterpretationAsync, cancellationToken);

    private static Task<IResult> SelectAngleAsync(
        Guid tenantId,
        Guid angleId,
        SelectOpportunityAngleCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, angleId, command, context, identity, timeProvider,
            commands.SelectAngleAsync, cancellationToken);

    private static Task<IResult> ResolveObjectionAsync(
        Guid tenantId,
        Guid objectionId,
        ResolveCriticObjectionCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, objectionId, command, context, identity, timeProvider,
            commands.ResolveObjectionAsync, cancellationToken);

    private static Task<IResult> SubmitStrategyAsync(
        Guid tenantId,
        Guid strategyId,
        SubmitStrategyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, strategyId, command, context, identity, timeProvider,
            commands.SubmitStrategyAsync, cancellationToken);

    private static Task<IResult> ApproveStrategyAsync(
        Guid tenantId,
        Guid strategyId,
        ApproveStrategyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, strategyId, command, context, identity, timeProvider,
            commands.ApproveStrategyAsync, cancellationToken);

    private static Task<IResult> RejectStrategyAsync(
        Guid tenantId,
        Guid strategyId,
        RejectStrategyCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, strategyId, command, context, identity, timeProvider,
            commands.RejectStrategyAsync, cancellationToken);

    private static Task<IResult> ResumeRunAsync(
        Guid tenantId,
        Guid runId,
        ManageAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ManageRunAsync(
            tenantId, runId, false, command, context, identity, commands,
            timeProvider, cancellationToken);

    private static Task<IResult> CancelRunAsync(
        Guid tenantId,
        Guid runId,
        ManageAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ManageRunAsync(
            tenantId, runId, true, command, context, identity, commands,
            timeProvider, cancellationToken);

    private static Task<IResult> ManageRunAsync(
        Guid tenantId,
        Guid runId,
        bool cancel,
        ManageAgentRunCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityWorkflowCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => ExecuteVersionedAsync(
            tenantId, runId, command, context, identity, timeProvider,
            (id, envelope, token) => commands.ManageRunAsync(id, cancel, envelope, token),
            cancellationToken);

    private static Task<IResult> ExecuteVersionedAsync<TCommand, TResult>(
        Guid tenantId,
        Guid resourceId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider timeProvider,
        Func<Guid, CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull
    {
        return CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => execute(resourceId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);
    }
}
