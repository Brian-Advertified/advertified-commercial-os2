using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class InventorySupplierLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapSupplierClaimEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Inventory suppliers")
            .RequireAuthorization();

        group.MapGet("/inventory-suppliers/{supplierId:guid}", GetSupplierAsync)
            .WithName("GetInventorySupplierLifecycle")
            .Produces<InventorySupplierLifecycleView>()
            .WithQueryProblems();
        group.MapPost(
                "/inventory-suppliers/{supplierId:guid}/claim-invitations",
                IssueInvitationAsync)
            .WithName("IssueSupplierClaimInvitation")
            .Produces<SupplierClaimInvitationView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost(
                "/supplier-claim-invitations/{invitationId:guid}:revoke",
                RevokeInvitationAsync)
            .WithName("RevokeSupplierClaimInvitation")
            .Produces<SupplierClaimInvitationView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost(
                "/supplier-claim-invitations/{invitationId:guid}:accept",
                AcceptInvitationAsync)
            .WithName("AcceptSupplierClaimInvitation")
            .Produces<SupplierClaimInvitationView>()
            .WithCommandProblems(requiresVersion: false);

        return endpoints;
    }

    public static IEndpointRouteBuilder MapInventoryReleaseEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Inventory releases")
            .RequireAuthorization();

        group.MapPost(
                "/inventory-imports/{importId:guid}:resolve-supplier",
                ResolveImportSupplierAsync)
            .WithName("ResolveInventoryImportSupplier")
            .Produces<InventoryImportView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapGet(
                "/proposal-versions/{proposalVersionId:guid}/inventory-impacts",
                ListProposalImpactsAsync)
            .WithName("ListProposalInventoryImpacts")
            .Produces<IReadOnlyList<ProposalInventoryImpactView>>()
            .WithQueryProblems();
        group.MapPost(
                "/proposal-inventory-impacts/{impactId:guid}:resolve",
                ResolveProposalImpactAsync)
            .WithName("ResolveProposalInventoryImpact")
            .Produces<ProposalInventoryImpactView>()
            .WithCommandProblems(requiresVersion: true);

        return endpoints;
    }

    private static async Task<IResult> GetSupplierAsync(
        Guid tenantId,
        Guid supplierId,
        HttpContext context,
        ICurrentIdentity identity,
        IInventorySupplierLifecycleReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetSupplierAsync(
            identity.ActorId, new TenantId(tenantId), supplierId,
            cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static Task<IResult> IssueInvitationAsync(
        Guid tenantId,
        Guid supplierId,
        IssueSupplierClaimInvitationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventorySupplierLifecycleCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true,
            (envelope, token) => commands.IssueInvitationAsync(
                supplierId, envelope, token),
            cancellationToken);

    private static Task<IResult> RevokeInvitationAsync(
        Guid tenantId,
        Guid invitationId,
        RevokeSupplierClaimInvitationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventorySupplierLifecycleCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true,
            (envelope, token) => commands.RevokeInvitationAsync(
                invitationId, envelope, token),
            cancellationToken);

    private static Task<IResult> AcceptInvitationAsync(
        Guid tenantId,
        Guid invitationId,
        AcceptSupplierClaimInvitationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventorySupplierLifecycleCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: false,
            (envelope, token) => commands.AcceptInvitationAsync(
                invitationId, envelope, token),
            cancellationToken);

    private static Task<IResult> ResolveImportSupplierAsync(
        Guid tenantId,
        Guid importId,
        ResolveInventoryImportSupplierCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventoryCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true,
            (envelope, token) => commands.ResolveSupplierAsync(
                importId, envelope, token),
            cancellationToken);

    private static Task<IReadOnlyList<ProposalInventoryImpactView>>
        ListProposalImpactsAsync(
            Guid tenantId,
            Guid proposalVersionId,
            ICurrentIdentity identity,
            IInventorySupplierLifecycleReader reader,
            CancellationToken cancellationToken) =>
        reader.ListProposalImpactsAsync(
            identity.ActorId, new TenantId(tenantId), proposalVersionId,
            cancellationToken);

    private static Task<IResult> ResolveProposalImpactAsync(
        Guid tenantId,
        Guid impactId,
        ResolveProposalInventoryImpactCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IInventorySupplierLifecycleCommands commands,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) =>
        CommandEndpointExecutor.ExecuteOkAsync(
            tenantId, command, context, identity, timeProvider,
            requireVersion: true,
            (envelope, token) => commands.ResolveProposalImpactAsync(
                impactId, envelope, token),
            cancellationToken);
}
