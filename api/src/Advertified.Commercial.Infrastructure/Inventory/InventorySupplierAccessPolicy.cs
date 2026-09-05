using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySupplierAccessPolicy(
    GovernanceDbContext dbContext, ITenantAuthorizer authorizer)
{
    public async Task EnsureUploadAccessAsync(
        ActorId actorId, TenantId tenantId, Guid? productId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.InventoryImport, cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Tenant access denied.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        if (productId.HasValue)
            await EnsureProductAccessAsync(actorId, tenantId, productId.Value, cancellationToken);
        else
            await ResolveSupplierScopeAsync(actorId, tenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task<Guid?> ResolveUploadSupplierAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid? requestedSupplierId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        if (scope is null)
        {
            return requestedSupplierId;
        }
        if (requestedSupplierId.HasValue)
        {
            EnsureContains(scope, requestedSupplierId.Value);
            return requestedSupplierId;
        }
        return scope.Length == 1
            ? scope[0]
            : throw new UnauthorizedAccessException(
                "Select one of your supplier organisations before uploading inventory.");
    }

    internal async Task EnsureImportAccessAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        if (scope is null)
        {
            return;
        }
        var row = await dbContext.Database.SqlQuery<SupplierResourceRow>($"""
            SELECT supplier_id AS "SupplierId"
            FROM commercial.inventory_imports
            WHERE tenant_id = {tenantId.Value} AND id = {importId}
              AND soft_deleted_at_utc IS NULL
            """).SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (!row.SupplierId.HasValue)
        {
            throw new UnauthorizedAccessException("Inventory import access denied.");
        }
        EnsureContains(scope, row.SupplierId.Value);
    }

    internal async Task EnsureProductAccessAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        if (scope is null)
        {
            return;
        }
        var supplierId = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT supplier_id AS "Value"
            FROM commercial.inventory_products
            WHERE tenant_id = {tenantId.Value} AND id = {productId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (supplierId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Inventory product access denied.");
        }
        EnsureContains(scope, supplierId);
    }

    internal async Task EnsureAssetAccessAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        if (scope is null)
        {
            return;
        }
        var supplierId = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT product.supplier_id AS "Value"
            FROM commercial.inventory_assets asset
            JOIN commercial.inventory_product_versions version
              ON version.tenant_id = asset.tenant_id
             AND version.id = asset.product_version_id
            JOIN commercial.inventory_products product
              ON product.tenant_id = version.tenant_id
             AND product.id = version.product_id
            WHERE asset.tenant_id = {tenantId.Value} AND asset.id = {assetId}
            """).SingleOrDefaultAsync(cancellationToken);
        if (supplierId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Inventory asset access denied.");
        }
        EnsureContains(scope, supplierId);
    }

    internal async Task EnsureSupplierAccessAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var scope = await ResolveSupplierScopeAsync(
            actorId, tenantId, cancellationToken);
        if (scope is not null)
        {
            EnsureContains(scope, supplierId);
        }
    }

    internal async Task<Guid[]?> ResolveSupplierScopeAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.Database.SqlQuery<string>($"""
            SELECT role_code AS "Value"
            FROM commercial.memberships
            WHERE tenant_id = {tenantId.Value} AND user_id = {actorId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
            """).SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Tenant access denied.");
        if (role == MasterDataCodes.Roles.SupplierAdmin)
            throw new UnauthorizedAccessException("The retired supplier role cannot access inventory.");
        if (role != MasterDataCodes.Roles.SupplierUser)
        {
            return null;
        }
        var supplierIds = await dbContext.Database.SqlQuery<Guid>($"""
            SELECT supplier_id AS "Value"
            FROM commercial.inventory_supplier_memberships
            WHERE tenant_id = {tenantId.Value} AND user_id = {actorId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND role_code = {MasterDataCodes.Roles.SupplierUser}
            ORDER BY supplier_id
            """).ToListAsync(cancellationToken);
        var scope = supplierIds.Distinct().ToArray();
        return scope.Length > 0
            ? scope
            : throw new UnauthorizedAccessException("Supplier access denied.");
    }

    private static void EnsureContains(Guid[] scope, Guid supplierId)
    {
        if (!scope.Contains(supplierId))
        {
            throw new UnauthorizedAccessException("Supplier access denied.");
        }
    }

    private sealed record SupplierResourceRow(Guid? SupplierId);
}
