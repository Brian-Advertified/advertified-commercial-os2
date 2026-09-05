using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySupplierLifecycleReader(
    InventorySupplierLifecycleStore store,
    InventorySupplierAccessPolicy supplierAccess,
    ITenantAuthorizer authorizer) : IInventorySupplierLifecycleReader
{
    public async Task<InventorySupplierLifecycleView> GetSupplierAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(actorId, tenantId,
            MasterDataReferences.Permissions.InventoryView, cancellationToken);
        var management = await authorizer.AuthorizeAsync(actorId, tenantId,
            MasterDataReferences.Permissions.SupplierClaimManage, cancellationToken);
        await using var transaction = await store.InventoryStore.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        await supplierAccess.EnsureSupplierAccessAsync(actorId, tenantId, supplierId, cancellationToken);
        var supplier = await store.FindSupplierAsync(
            tenantId, supplierId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Supplier access denied.");
        var view = await store.BuildSupplierViewAsync(
            tenantId, supplier, cancellationToken, includeInvitations: management.IsAllowed);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<IReadOnlyList<ProposalInventoryImpactView>> ListProposalImpactsAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proposalVersionId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            actorId, tenantId, MasterDataReferences.Permissions.ProposalView,
            cancellationToken);
        await using var transaction = await store.InventoryStore.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var impacts = await store.ListImpactsAsync(
            tenantId, proposalVersionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return impacts.Select(item => item.ToView()).ToArray();
    }

    private async Task EnsurePermissionAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Supplier access denied.");
        }
    }
}
