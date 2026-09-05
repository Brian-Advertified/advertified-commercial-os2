using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ResolveSupplierOutcomeAsync(
        Guid importId,
        CommandEnvelope<ResolveInventoryImportSupplierCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if (source.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired)
        {
            throw new InvalidLifecycleTransitionException();
        }
        var reason = OpportunityCommandSupport.Required(
            envelope.Command.Reason, 1000, nameof(envelope.Command.Reason));
        var resolution = await supplierIdentity.ResolveManualAsync(
            envelope.TenantId, source.Id, envelope.Command.ExistingSupplierId,
            envelope.Command.SupplierName, envelope.ActorId.Value, reason,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        await PersistSupplierResolutionAsync(
            source, envelope, resolution, now, cancellationToken);
        if (resolution.SupplierCreated && resolution.SupplierId.HasValue)
        {
            await BindCreatedSupplierToImportAsync(
                envelope.TenantId, resolution.SupplierId.Value, source.Id,
                cancellationToken);
        }
        var updated = await store.FindImportAsync(
            envelope.TenantId, source.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, source.Id, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryImportSupplierResolved,
            MasterDataReferences.CommercialEventTypes.InventoryImportSupplierResolved,
            now);
    }

    private async Task PersistSupplierResolutionAsync(
        InventoryImportRow source,
        CommandEnvelope<ResolveInventoryImportSupplierCommand> envelope,
        InventorySupplierResolution resolution,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET supplier_id = {resolution.SupplierId},
                supplier_name_hint = {resolution.SupplierName},
                supplier_resolution_status_code = {resolution.Status},
                supplier_identity_evidence_json = {resolution.EvidenceJson}::jsonb,
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {envelope.TenantId.Value} AND id = {source.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
    }
}
