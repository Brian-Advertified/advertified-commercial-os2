using System.Security.Cryptography;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> CreateOutcomeAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var command = envelope.Command;
        var supplierName = OpportunityCommandSupport.Required(
            command.SupplierName, 300, nameof(command.SupplierName));
        var fileName = Path.GetFileName(OpportunityCommandSupport.Required(
            command.Source.FileName, 500, nameof(command.Source.FileName)));
        var declared = OpportunityCommandSupport.Required(
            command.Source.DeclaredMediaType, 200, nameof(command.Source.DeclaredMediaType));
        var detected = InventoryDocumentClassifier.Detect(
            fileName, declared, command.Source.Content, maximumSourceBytes);
        var id = Guid.NewGuid();
        var hash = Convert.ToHexStringLower(SHA256.HashData(command.Source.Content));
        var quarantineKey = $"quarantine/{envelope.TenantId.Value:N}/{id:N}/{hash}";
        await store.ObjectStore.PutAsync(
            quarantineKey, command.Source.Content, detected.MediaType, cancellationToken);
        var scan = await store.MalwareScanner.ScanAsync(command.Source.Content, cancellationToken);
        var supplierId = await EnsureSupplierAsync(
            envelope.TenantId, supplierName, timeProvider.GetUtcNow(), cancellationToken);
        var now = timeProvider.GetUtcNow();
        var protectedKey = scan.IsClean
            ? $"protected/{envelope.TenantId.Value:N}/{hash}" : null;
        if (protectedKey is not null)
        {
            await store.ObjectStore.PutAsync(
                protectedKey, command.Source.Content, detected.MediaType, cancellationToken);
        }
        var status = scan.IsClean ? MasterDataCodes.LifecycleStatuses.Uploaded : MasterDataCodes.LifecycleStatuses.Failed;
        var scanStatus = scan.IsClean ? MasterDataCodes.MalwareScanStatuses.Clean : MasterDataCodes.MalwareScanStatuses.Infected;
        var failure = scan.IsClean ? null : "MALWARE_DETECTED";
        await InsertImportAsync(envelope, id, supplierId, fileName, declared, detected.Code,
            status, scanStatus, quarantineKey, protectedKey, hash, failure, now, cancellationToken);
        await RecordStepAsync(envelope.TenantId, id, MasterDataCodes.InventoryImportStepTypes.UploadProtection,
            scan.IsClean ? MasterDataCodes.LifecycleStatuses.Completed : MasterDataCodes.LifecycleStatuses.Failed,
            now, cancellationToken);
        await RecordStepAsync(envelope.TenantId, id, MasterDataCodes.InventoryImportStepTypes.Classification,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
        var row = await store.FindImportAsync(envelope.TenantId, id, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1, MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryImportCreated,
            MasterDataReferences.CommercialEventTypes.InventoryImportCreated, now);
    }

    private async Task<Guid> EnsureSupplierAsync(
        TenantId tenantId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_suppliers (
                id, tenant_id, name, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {tenantId.Value}, {name}, 1, {now}, {now})
            ON CONFLICT (tenant_id, (lower(name))) DO NOTHING
            """, cancellationToken);
        return await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT id AS "Value" FROM commercial.inventory_suppliers
            WHERE tenant_id = {tenantId.Value} AND lower(name) = lower({name})
            """).SingleAsync(cancellationToken);
    }

    private Task<int> InsertImportAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        Guid id, Guid supplierId, string fileName, string declared, string documentClass,
        string status, string scanStatus, string quarantineKey, string? protectedKey,
        string hash, string? failure, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, source_file_name, declared_media_type,
                document_class_collection_code, document_class_code, status_code,
                scan_status_code, quarantine_object_key, protected_object_key, source_hash,
                source_size, failure_code, created_by, version, created_at_utc, updated_at_utc)
            VALUES (
                {id}, {envelope.TenantId.Value}, {supplierId}, {fileName}, {declared},
                {MasterDataCodes.DocumentClasses.Collection}, {documentClass}, {status}, {scanStatus}, {quarantineKey},
                {protectedKey}, {hash}, {envelope.Command.Source.Content.LongLength}, {failure},
                {envelope.ActorId.Value}, 1, {now}, {now})
            """, cancellationToken);

    private Task<int> RecordStepAsync(
        TenantId tenantId,
        Guid importId,
        string step,
        string status,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code, status_code,
                outcome_json, started_at_utc, completed_at_utc)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {importId}, {step}, {status},
                {"{}"}::jsonb, {now}, {now})
            ON CONFLICT (tenant_id, import_id, step_type_code) DO NOTHING
            """, cancellationToken);
}
