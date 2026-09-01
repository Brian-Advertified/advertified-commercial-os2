using Advertified.Commercial.Api.Authentication;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Proposal;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class ProposalEndpoints
{
    public static IEndpointRouteBuilder MapProposalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Proposals and client decisions")
            .RequireAuthorization();
        group.MapGet("/briefs/{briefId:guid}/approved-plans", ListApprovedPlansAsync)
            .WithName("ListApprovedPlansForProposal")
            .Produces<IReadOnlyList<ApprovedPlanChoiceView>>()
            .WithQueryProblems();
        group.MapPost("/briefs/{briefId:guid}/proposals:generate", GenerateAsync)
            .WithName("GenerateProposal").Produces<ProposalVersionView>()
            .RequireRateLimiting(RequestRateLimitPolicies.AgentWork)
            .WithCommandProblems(requiresVersion: false);
        group.MapGet("/proposal-approvers", ListApproversAsync)
            .WithName("ListProposalApprovers")
            .Produces<IReadOnlyList<ProposalApproverView>>()
            .WithQueryProblems();
        group.MapGet("/proposal-recipients", ListRecipientsAsync)
            .WithName("ListProposalRecipients")
            .Produces<IReadOnlyList<ProposalRecipientView>>()
            .WithQueryProblems();
        group.MapGet("/proposals/{proposalVersionId:guid}", GetAsync)
            .WithName("GetProposal").Produces<ProposalVersionView>()
            .WithQueryProblems();
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:update", UpdateAsync)
            .WithName("UpdateProposal").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:submit", SubmitForApprovalAsync)
            .WithName("SubmitProposalForApproval").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:approve", ApproveAsync)
            .WithName("ApproveProposal").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:reject", RejectApprovalAsync)
            .WithName("RejectProposalApproval").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:render", RenderAsync)
            .WithName("RenderProposal").Produces<ProposalVersionView>()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:share", ShareAsync)
            .WithName("ShareProposal")
            .Produces<ProposalVersionView>()
            .RequireRateLimiting(RequestRateLimitPolicies.HeavyWork)
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:select-option", SelectAsync)
            .WithName("SelectProposalOption").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/proposal-versions/{proposalVersionId:guid}:decline", DeclineAsync)
            .WithName("DeclineProposal").Produces<ProposalVersionView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet("/proposal-documents/{documentId:guid}", GetDocumentAsync)
            .WithName("GetProposalDocument")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .WithQueryProblems();
        return endpoints;
    }

    private static Task<IResult> GenerateAsync(
        Guid tenantId, Guid briefId, GenerateProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, false,
            (envelope, token) => commands.GenerateAsync(briefId, envelope, token),
            cancellationToken);

    private static Task<IResult> UpdateAsync(
        Guid tenantId, Guid proposalVersionId, UpdateProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.UpdateAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static Task<IResult> SubmitForApprovalAsync(
        Guid tenantId, Guid proposalVersionId, SubmitProposalForApprovalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.SubmitForApprovalAsync(
                proposalVersionId, envelope, token), cancellationToken);

    private static Task<IResult> ApproveAsync(
        Guid tenantId, Guid proposalVersionId, ApproveProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ApproveAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static Task<IResult> RejectApprovalAsync(
        Guid tenantId, Guid proposalVersionId, RejectProposalApprovalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.RejectApprovalAsync(
                proposalVersionId, envelope, token), cancellationToken);

    private static Task<IResult> RenderAsync(
        Guid tenantId, Guid proposalVersionId, RenderProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.RenderAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static Task<IResult> ShareAsync(
        Guid tenantId, Guid proposalVersionId, ShareProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.ShareAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static Task<IResult> SelectAsync(
        Guid tenantId, Guid proposalVersionId, SelectProposalOptionCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.SelectOptionAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static Task<IResult> DeclineAsync(
        Guid tenantId, Guid proposalVersionId, DeclineProposalCommand command,
        HttpContext context, ICurrentIdentity identity, IProposalCommands commands,
        TimeProvider clock, CancellationToken cancellationToken) => ExecuteAsync(
            tenantId, command, context, identity, clock, true,
            (envelope, token) => commands.DeclineAsync(proposalVersionId, envelope, token),
            cancellationToken);

    private static async Task<IResult> ListApproversAsync(
        Guid tenantId, ICurrentIdentity identity,
        IProposalReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListApproversAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> ListRecipientsAsync(
        Guid tenantId, ICurrentIdentity identity,
        IProposalReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListRecipientsAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid tenantId, Guid proposalVersionId, ICurrentIdentity identity,
        IProposalReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), proposalVersionId, cancellationToken));

    private static async Task<IResult> ListApprovedPlansAsync(
        Guid tenantId, Guid briefId, ICurrentIdentity identity,
        IProposalReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListApprovedPlansAsync(
            identity.ActorId, new TenantId(tenantId), briefId, cancellationToken));

    private static async Task<IResult> GetDocumentAsync(
        Guid tenantId, Guid documentId, ICurrentIdentity identity,
        IProposalReader reader, CancellationToken cancellationToken)
    {
        var document = await reader.GetDocumentAsync(
            identity.ActorId, new TenantId(tenantId), documentId, cancellationToken);
        return Results.File(document.Content, document.MediaType, document.FileName);
    }

    private static Task<IResult> ExecuteAsync<TCommand>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        TimeProvider clock,
        bool requireVersion,
        Func<CommandEnvelope<TCommand>, CancellationToken,
            Task<Advertified.Commercial.Application.Foundation.CommandResult<ProposalVersionView>>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull => CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, clock,
            requireVersion, execute, cancellationToken);
}
