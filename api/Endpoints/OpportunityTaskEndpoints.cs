using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityTaskEndpoints
{
    public static RouteGroupBuilder MapOpportunityTaskCommands(this RouteGroupBuilder group)
    {
        group.MapPost("/human-tasks/{taskId:guid}:complete", CompleteTaskAsync)
            .WithName("CompleteHumanTask")
            .Produces<HumanTaskCompletionView>()
            .WithCommandProblems(requiresVersion: true);
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
        IBriefCommands briefCommands,
        IProposalCommands proposalCommands,
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
            MasterDataCodes.HumanTaskTypes.EvidenceItemReview => await CompleteEvidenceItemAsync(
                task, request, context, tenant, identity, opportunityCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.EvidenceSetApproval => await CompleteEvidenceSetAsync(
                task, request, context, tenant, identity, opportunityCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.InterpretationConfirmation => await CompleteInterpretationAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.AngleSelection => await CompleteAngleAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.CriticResolution => await CompleteObjectionAsync(
                task, request, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.StrategyApproval => await CompleteStrategyAsync(
                task, request, action, context, tenant, identity, workflowCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.BriefApproval => await CompleteBriefAsync(
                task, request, action, context, tenant, identity, briefCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.ProposalApproval => await CompleteProposalAsync(
                task, request, action, context, tenant, identity, proposalCommands,
                timeProvider, cancellationToken),
            MasterDataCodes.HumanTaskTypes.RunRecovery => await CompleteRunAsync(
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
                request.Decision ?? MasterDataCodes.EvidenceReviewDecisions.Approve,
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
        if (action is not (MasterDataCodes.EvidenceReviewDecisions.Approve or MasterDataCodes.EvidenceReviewDecisions.Reject))
        {
            throw new ArgumentException("The strategy action must be APPROVE or REJECT.");
        }
        return action == MasterDataCodes.EvidenceReviewDecisions.Reject
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

    private static Task<IResult> CompleteProposalAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, string action, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken token)
    {
        if (action is not (MasterDataCodes.EvidenceReviewDecisions.Approve or
            MasterDataCodes.EvidenceReviewDecisions.Reject))
        {
            throw new ArgumentException("The proposal action must be APPROVE or REJECT.");
        }
        return action == MasterDataCodes.EvidenceReviewDecisions.Reject
            ? ExecuteAsync(
                task,
                new RejectProposalApprovalCommand(
                    request.Reason ?? throw new ArgumentException("A rejection reason is required.")),
                context, tenant, identity, clock,
                (envelope, cancellation) => commands.RejectApprovalAsync(
                    task.ResourceId, envelope, cancellation), token)
            : ExecuteAsync(
                task, new ApproveProposalCommand(request.Reason),
                context, tenant, identity, clock,
                (envelope, cancellation) => commands.ApproveAsync(
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

    private static Task<IResult> CompleteBriefAsync(
        HumanTaskView task, CompleteHumanTaskRequest request, string action, HttpContext context,
        TenantId tenant, ICurrentIdentity identity, IBriefCommands commands,
        TimeProvider clock, CancellationToken token)
    {
        if (action is not ("CONFIRM" or MasterDataCodes.EvidenceReviewDecisions.Approve
            or MasterDataCodes.EvidenceReviewDecisions.Reject))
        {
            throw new ArgumentException("The Brief action must be CONFIRM or REJECT.");
        }
        return action == MasterDataCodes.EvidenceReviewDecisions.Reject
            ? ExecuteAsync(
                task,
                new RejectBriefVersionCommand(
                    request.Reason ?? throw new ArgumentException("A rejection reason is required."),
                    request.RequestedChanges ?? throw new ArgumentException(
                        "Requested changes are required.")),
                context, tenant, identity, clock,
                (envelope, cancellation) => commands.RejectAsync(
                    task.ResourceId, envelope, cancellation), token)
            : ExecuteAsync(
                task, new ApproveBriefVersionCommand(request.Reason),
                context, tenant, identity, clock,
                (envelope, cancellation) => commands.ApproveAsync(
                    task.ResourceId, envelope, cancellation), token);
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
            task.Id, task.TaskType, MasterDataCodes.LifecycleStatuses.Completed, task.ResourceId, result.Version));
    }
}

public sealed record CompleteHumanTaskRequest(
    string Action,
    Guid? SelectedResourceId,
    string? Decision,
    string? StructuredValueJson,
    string? Resolution,
    string? Reason,
    string? RequestedChanges = null);
