using System.Security.Cryptography;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
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
        var selectedSupplierId = await supplierAccess.ResolveUploadSupplierAsync(
            envelope.ActorId, envelope.TenantId, command.ExistingSupplierId,
            cancellationToken);
        var supplierHint = OpportunityCommandSupport.Optional(
            command.SupplierName, 300, nameof(command.SupplierName));
        var replacementMode = ValidateReplacementMode(command.ReplacementMode);
        var fileName = Path.GetFileName(OpportunityCommandSupport.Required(
            command.Source.FileName, 500, nameof(command.Source.FileName)));
        var declared = OpportunityCommandSupport.Required(
            command.Source.DeclaredMediaType, 200, nameof(command.Source.DeclaredMediaType));
        var detected = InventoryDocumentClassifier.Detect(
            fileName, declared, command.Source.Content, maximumSourceBytes);
        var id = Guid.NewGuid();
        var hash = Convert.ToHexStringLower(SHA256.HashData(command.Source.Content));
        var quarantineKey = $"quarantine/{envelope.TenantId.Value:N}/{id:N}/{hash}";
        var scan = await store.MalwareScanner.ScanAsync(
            command.Source.Content, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var supplier = scan.IsClean || selectedSupplierId.HasValue
            ? await supplierIdentity.ResolveHintAsync(
                envelope.TenantId, selectedSupplierId, supplierHint,
                envelope.ActorId.Value, cancellationToken)
            : PendingSupplierResolution();
        var protectedKey = scan.IsClean
            ? $"protected/{envelope.TenantId.Value:N}/{hash}" : null;
        await StoreSourceAsync(
            command, detected.MediaType, quarantineKey, protectedKey,
            cancellationToken);
        var status = scan.IsClean
            ? MasterDataCodes.LifecycleStatuses.Uploaded
            : MasterDataCodes.LifecycleStatuses.Failed;
        var scanStatus = scan.IsClean
            ? MasterDataCodes.MalwareScanStatuses.Clean
            : MasterDataCodes.MalwareScanStatuses.Infected;
        var failure = scan.IsClean ? null : "MALWARE_DETECTED";
        await InsertImportAsync(
            envelope, id, supplier, supplierHint, replacementMode,
            fileName, declared, detected.Code, status, scanStatus,
            quarantineKey, protectedKey, hash, failure, now, cancellationToken);
        if (supplier.SupplierCreated && supplier.SupplierId.HasValue)
        {
            await BindCreatedSupplierToImportAsync(
                envelope.TenantId, supplier.SupplierId.Value, id,
                cancellationToken);
        }
        await RecordInitialStepsAsync(
            envelope.TenantId, id, scan.IsClean, now, cancellationToken);
        var row = await store.FindImportAsync(
            envelope.TenantId, id, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(row, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, id, 1,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryImportCreated,
            MasterDataReferences.CommercialEventTypes.InventoryImportCreated, now);
    }

    private async Task StoreSourceAsync(
        CreateInventoryImportCommand command,
        string mediaType,
        string quarantineKey,
        string? protectedKey,
        CancellationToken cancellationToken)
    {
        var objectKey = protectedKey ?? quarantineKey;
        await store.ObjectStore.PutAsync(
            objectKey, command.Source.Content, mediaType, cancellationToken);
    }

    private async Task RecordInitialStepsAsync(
        TenantId tenantId,
        Guid importId,
        bool clean,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await RecordStepAsync(
            tenantId, importId,
            MasterDataCodes.InventoryImportStepTypes.UploadProtection,
            clean ? MasterDataCodes.LifecycleStatuses.Completed
                : MasterDataCodes.LifecycleStatuses.Failed,
            now, cancellationToken);
        await RecordStepAsync(
            tenantId, importId,
            MasterDataCodes.InventoryImportStepTypes.Classification,
            MasterDataCodes.LifecycleStatuses.Completed,
            now, cancellationToken);
    }

    private static string ValidateReplacementMode(string replacementMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementMode);
        return replacementMode ==
            MasterDataCodes.InventoryReplacementModes.FullReplacement
            ? replacementMode
            : throw new ArgumentException(
                "Only a complete supplier inventory replacement is currently supported.",
                nameof(replacementMode));
    }

    private static InventorySupplierResolution PendingSupplierResolution() => new(
        null, "Supplier to be identified",
        MasterDataCodes.InventorySupplierResolutionStatuses.Pending,
        "{}", false);

    private Task<int> InsertImportAsync(
        CommandEnvelope<CreateInventoryImportCommand> envelope,
        Guid id,
        InventorySupplierResolution supplier,
        string? supplierHint,
        string replacementMode,
        string fileName,
        string declared,
        string documentClass,
        string status,
        string scanStatus,
        string quarantineKey,
        string? protectedKey,
        string hash,
        string? failure,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_imports (
                id, tenant_id, supplier_id, supplier_name_hint,
                supplier_resolution_status_code,
                supplier_identity_evidence_json, replacement_mode_code,
                source_file_name, declared_media_type,
                document_class_collection_code, document_class_code,
                status_code, scan_status_code, quarantine_object_key,
                protected_object_key, source_hash, source_size, failure_code,
                created_by, version, created_at_utc, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {supplier.SupplierId},
                {supplierHint}, {supplier.Status}, {supplier.EvidenceJson}::jsonb,
                {replacementMode}, {fileName}, {declared},
                {MasterDataCodes.DocumentClasses.Collection}, {documentClass},
                {status}, {scanStatus}, {quarantineKey}, {protectedKey}, {hash},
                {envelope.Command.Source.Content.LongLength}, {failure},
                {envelope.ActorId.Value}, 1, {now}, {now})
            """, cancellationToken);

    private Task<int> BindCreatedSupplierToImportAsync(
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_suppliers
            SET created_from_import_id = {importId}
            WHERE tenant_id = {tenantId.Value} AND id = {supplierId}
              AND created_from_import_id IS NULL
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
