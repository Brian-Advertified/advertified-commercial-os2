using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventorySchemaExecutionGuard(InventoryRecordStore store,
    InventorySupplierAccessPolicy supplierAccess, ITenantAuthorizer authorizer, TimeProvider clock)
{
    internal async Task<(InventorySchemaExecutionContext Context, InventoryCodeSets Codes)> PrepareAsync(
        InventoryExtractionWorkerClaim claim, CancellationToken cancellationToken)
    {
        var actor = new ActorId(claim.RequestedBy);
        var tenant = new TenantId(claim.TenantId);
        var decision = await authorizer.AuthorizeAsync(actor, tenant,
            MasterDataReferences.Permissions.InventoryImport, cancellationToken);
        if (!decision.IsAllowed) throw new UnauthorizedAccessException("Inventory import access denied.");
        await using var transaction = await store.BeginSessionAsync(actor, tenant, cancellationToken);
        await supplierAccess.EnsureImportAccessAsync(actor, tenant, claim.ImportId, cancellationToken);
        var source = await store.FindImportAsync(tenant, claim.ImportId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Status != MasterDataCodes.LifecycleStatuses.Extracting || source.SourceHash != claim.SourceHash ||
            !await IsCurrentAsync(claim, cancellationToken))
            throw new InvalidLifecycleTransitionException();
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var context = new InventorySchemaExecutionContext(claim.TenantId, claim.RequestedBy,
            claim.ImportId, source.Version, claim.AttemptId, claim.CorrelationId);
        await transaction.CommitAsync(cancellationToken);
        return (context, codes);
    }

    private Task<bool> IsCurrentAsync(InventoryExtractionWorkerClaim claim, CancellationToken cancellationToken) =>
        store.DbContext.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
                SELECT 1 FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.tenant_id = {claim.TenantId} AND attempt.id = {claim.AttemptId}
                  AND attempt.import_id = {claim.ImportId}
                  AND attempt.status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
                  AND attempt.worker_lease_token = {claim.ClaimToken}
                  AND attempt.worker_lease_expires_at_utc > {clock.GetUtcNow()}
                  AND NOT EXISTS (
                      SELECT 1 FROM commercial.inventory_extraction_attempts newer
                      WHERE newer.tenant_id = attempt.tenant_id AND newer.import_id = attempt.import_id
                        AND newer.attempt_number > attempt.attempt_number)) AS "Value"
            """).SingleAsync(cancellationToken);
}
