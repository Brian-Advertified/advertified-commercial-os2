using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityTaskEndpoints
{
    public static RouteGroupBuilder MapOpportunityTaskCommands(this RouteGroupBuilder group)
    {
        group.MapPost("/human-tasks/{taskId:guid}:complete", CompleteTaskAsync)
            .WithName("CompleteHumanTask")
            .Produces<HumanTaskCompletionView>()
            .WithGate4CommandProblems(requiresVersion: true);
        return group;
    }

    private static async Task<IResult> CompleteTaskAsync(
        Guid tenantId,
        Guid taskId,
        CompleteHumanTaskRequest request,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityReader reader,
        IOpportunityCommands opportunityCommands,
        IOpportunityWorkflowCommands workflowCommands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var tenant = new TenantId(tenantId);
        var task = await reader.GetTaskAsync(
            identity.ActorId, tenant, taskId, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        var action = request.Action.Trim().ToUpperInvariant();
        return task.TaskType switch
        {
            Gate4TaskTypes.EvidenceItemReview => await CompleteEvidenceItemAsync(
                task, request, context, tenant, identity, opportunityCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.EvidenceSetApproval => await CompleteEvidenceSetAsync(
                task, request, context, tenant, identity, opportunityCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.InterpretationConfirmation => await CompleteInterpretationAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.AngleSelection => await CompleteAngleAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.CriticResolution => await CompleteObjectionAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.StrategyApproval => await CompleteStrategyAsync(
                task, request, action, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            Gate4TaskTypes.RunRecovery => await CompleteRunAsync(
                task, request, action, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            _ => throw new InvalidLifecycleTransitionException(),
        };
    }

    private static Task<IResult> CompleteEvidenceItemAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityCommands commands,
        TimeProvider clock, CancellationToken token) => ExecuteAsync(
            task, new ReviewEvidenceItemCommand(
                request.Decision ?? Gate4ReviewDecisions.Approve,
                request.StructuredValueJson,
                request.Reason),
            context, tenant, identity, clock,
            (envelope, cancellation) => commands.ReviewEvidenceItemAsync(
                task.ResourceId, envelope, cancellation), token);

    private static Task<IResult> CompleteEvidenceSetAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityCommands commands,
        TimeProvider clock, CancellationToken token) => ExecuteAsync(
            task, new ApproveEvidenceSetCommand(request.Reason), context, tenant, identity, clock,
            (envelope, cancellation) => commands.ApproveEvidenceSetAsync(
                task.ResourceId, envelope, cancellation), token);

    private static Task<IResult> CompleteInterpretationAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityWorkflowCommands commands,
        TimeProvider clock, CancellationToken token) => ExecuteAsync(
            task, new ConfirmInterpretationCommand(request.Reason), context, tenant, identity, clock,
            (envelope, cancellation) => commands.ConfirmInterpretationAsync(
                task.ResourceId, envelope, cancellation), token);

    private static Task<IResult> CompleteAngleAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityWorkflowCommands commands,
        TimeProvider clock, CancellationToken token)
    {
        var angleId = request.SelectedResourceId
            ?? throw new ArgumentException("A selected angle is required.");
        return ExecuteAsync(
            task, new SelectOpportunityAngleCommand(request.Reason), context, tenant, identity, clock,
            (envelope, cancellation) => commands.SelectAngleAsync(angleId, envelope, cancellation),
            token);
    }

    private static Task<IResult> CompleteObjectionAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityWorkflowCommands commands,
        TimeProvider clock, CancellationToken token) => ExecuteAsync(
            task,
            new ResolveCriticObjectionCommand(
                request.Resolution ?? throw new ArgumentException("A resolution is required."),
                request.Reason ?? throw new ArgumentException("A reason is required.")),
            context, tenant, identity, clock,
            (envelope, cancellation) => commands.ResolveObjectionAsync(
                task.ResourceId, envelope, cancellation), token);

    private static Task<IResult> CompleteStrategyAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, string action, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityWorkflowCommands commands,
        TimeProvider clock, CancellationToken token)
    {
        if (action is not (Gate4ReviewDecisions.Approve or Gate4ReviewDecisions.Reject))
        {
            throw new ArgumentException("The strategy action must be APPROVE or REJECT.");
        }
        return action == Gate4ReviewDecisions.Reject
        ? ExecuteAsync(
            task,
            new RejectStrategyCommand(
                request.Reason ?? throw new ArgumentException("A rejection reason is required.")),
            context, tenant, identity, clock,
            (envelope, cancellation) => commands.RejectStrategyAsync(
                task.ResourceId, envelope, cancellation), token)
        : ExecuteAsync(
            task, new ApproveStrategyCommand(request.Reason), context, tenant, identity, clock,
            (envelope, cancellation) => commands.ApproveStrategyAsync(
                task.ResourceId, envelope, cancellation), token);
    }

    private static Task<IResult> CompleteRunAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, string action, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IOpportunityWorkflowCommands commands,
        TimeProvider clock, CancellationToken token)
    {
        if (action is not ("RESUME" or "CANCEL"))
        {
            throw new ArgumentException("The run action must be RESUME or CANCEL.");
        }
        return ExecuteAsync(
            task, new ManageAgentRunCommand(request.Reason), context, tenant, identity, clock,
            (envelope, cancellation) => commands.ManageRunAsync(
                task.ResourceId, action == "CANCEL", envelope, cancellation), token);
    }

    private static async Task<IResult> ExecuteAsync<TCommand, TResult>(
        HumanTaskView task,
        TCommand command,
        HttpContext context,
        TenantId tenant,
        ICurrentIdentity identity,
        TimeProvider clock,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
        where TResult : notnull
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, tenant, identity.ActorId, command, clock, requireVersion: true);
        if (envelope.ExpectedVersion != task.ResourceVersion)
        {
            throw new Advertified.Commercial.Domain.Commercial.VersionConflictException();
        }
        var result = await execute(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return Results.Ok(new HumanTaskCompletionView(
            task.Id, task.TaskType, Gate4Statuses.Completed, task.ResourceId, result.Version));
    }
}

public sealed record CompleteHumanTaskRequest(
    string Action,
    Guid? SelectedResourceId,
    string? Decision,
    string? StructuredValueJson,
    string? Resolution,
    string? Reason);
