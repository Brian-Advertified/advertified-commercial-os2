using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Opportunity;

namespace Advertified.Commercial.Api.Endpoints;

public static class OpportunityCommandEndpoints
{
    public static RouteGroupBuilder MapOpportunityCommands(this RouteGroupBuilder group)
    {
        group.MapPost("/opportunities", CreateOpportunityAsync)
            .WithName("CreateOpportunity")
            .Produces<OpportunityView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPatch("/opportunities/{opportunityId:guid}", UpdateOpportunityAsync)
            .WithName("UpdateOpportunity")
            .Produces<OpportunityView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/opportunities/{opportunityId:guid}/evidence-sources", RegisterSourceAsync)
            .WithName("RegisterOpportunityEvidenceSource")
            .Produces<EvidenceSourceView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/opportunities/{opportunityId:guid}/qualification:start", StartQualificationAsync)
            .WithName("StartOpportunityQualification")
            .Produces<OpportunityView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/evidence-items/{itemId:guid}/review", ReviewEvidenceAsync)
            .WithName("ReviewEvidenceItem")
            .Produces<EvidenceItemView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/opportunities/{opportunityId:guid}/evidence:submit", SubmitEvidenceAsync)
            .WithName("SubmitOpportunityEvidence")
            .Produces<EvidenceSetView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/evidence-sets/{evidenceSetId:guid}:approve", ApproveEvidenceAsync)
            .WithName("ApproveOpportunityEvidence")
            .Produces<EvidenceSetView>()
            .WithCommandProblems(requiresVersion: true);
        return group;
    }

    private static Task<IResult> CreateOpportunityAsync(
        Guid tenantId,
        CreateOpportunityCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, false,
            commands.CreateAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/opportunities/{result.Data.Id}",
                result.Data),
            cancellationToken);

    private static Task<IResult> RegisterSourceAsync(
        Guid tenantId,
        Guid opportunityId,
        RegisterEvidenceSourceCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (command.OpportunityId != opportunityId)
        {
            throw new ArgumentException("The route and source opportunity must match.");
        }
        return CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, false,
            commands.RegisterEvidenceSourceAsync,
            result => Results.Created(
                $"/api/v1/tenants/{tenantId}/opportunities/{opportunityId}", result.Data),
            cancellationToken);
    }

    private static Task<IResult> UpdateOpportunityAsync(
        Guid tenantId,
        Guid opportunityId,
        UpdateOpportunityCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => commands.UpdateAsync(opportunityId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);

    private static Task<IResult> StartQualificationAsync(
        Guid tenantId,
        Guid opportunityId,
        StartQualificationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => commands.StartQualificationAsync(
                opportunityId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);

    private static Task<IResult> ReviewEvidenceAsync(
        Guid tenantId,
        Guid itemId,
        ReviewEvidenceItemCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => commands.ReviewEvidenceItemAsync(itemId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);

    private static Task<IResult> SubmitEvidenceAsync(
        Guid tenantId,
        Guid opportunityId,
        SubmitEvidenceCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => commands.SubmitEvidenceAsync(
                opportunityId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);

    private static Task<IResult> ApproveEvidenceAsync(
        Guid tenantId,
        Guid evidenceSetId,
        ApproveEvidenceSetCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IOpportunityCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => CommandEndpointExecutor.ExecuteAsync(
            tenantId, command, context, identity, timeProvider, true,
            (envelope, token) => commands.ApproveEvidenceSetAsync(
                evidenceSetId, envelope, token),
            result => Results.Ok(result.Data),
            cancellationToken);

}
