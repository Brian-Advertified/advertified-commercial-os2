using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.Opportunity;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ExecuteOutcomeAsync(
        Guid importId,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(
            envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Status != MasterDataCodes.LifecycleStatuses.Uploaded ||
            source.ScanStatus != MasterDataCodes.MalwareScanStatuses.Clean ||
            source.ProtectedObjectKey is null || source.DocumentClass is null)
        {
            throw new InvalidLifecycleTransitionException();
        }
        if (source.Version != envelope.ExpectedVersion)
        {
            throw new VersionConflictException();
        }
        if (extractionAdapter is IDurableInventoryDocumentExtractionAdapter durableProvider)
        {
            return await QueueDurableExtractionAsync(
                source, envelope, durableProvider, cancellationToken);
        }
        var content = await store.ObjectStore.ReadAsync(
            source.ProtectedObjectKey, cancellationToken);
        InventoryExtractionCompletionPolicy.VerifySource(content, source.SourceHash);
        var extraction = await extractionAdapter.ExtractAsync(
            new InventoryExtractionRequest(
                source.FileName, source.DeclaredMediaType, source.DocumentClass,
                source.SourceHash, content), cancellationToken);
        InventoryExtractionCompletionPolicy.VerifyResult(extraction, source.SourceHash);
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var artifactId = Guid.NewGuid();
        await InsertExtractionAsync(
            envelope.TenantId, source.Id, artifactId,
            extraction, source.Version, cancellationToken);
        var rows = extraction.Rows;
        var now = timeProvider.GetUtcNow();
        var supplier = InventorySupplierIdentityService.ResolveExtraction(source, extraction);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            rows,
            source.SourceHash,
            supplier.SupplierName,
            codes,
            now);
        candidates = InventoryAcceptancePolicy.Apply(extraction, source.SourceHash,
            source.Version, codes, candidates, now);
        var documentReview = extraction.Document.DiscoveredSchema is null || candidates.Length == 0;
        Guid? reviewer = documentReview || candidates.Any(InventoryCandidateReviewPolicy.RequiresReview)
            ? await InventoryReviewerAssignment.FindAsync(
                store.DbContext, source.TenantId, source.CreatedBy, cancellationToken)
            : null;
        await InventoryProjectionPersistence.InsertInitialAsync(
            store.DbContext, envelope.TenantId, source.Id,
            artifactId, null, extraction, candidates.Length,
            envelope.ActorId.Value, now, cancellationToken);
        await InventoryCandidateBatchPersistence.PersistAsync(
            store.DbContext, envelope.TenantId, source.Id,
            artifactId, reviewer, now, candidates,
            cancellationToken);
        await CompleteExecutionAsync(
            envelope.TenantId, importId, source.Version, supplier,
            now, cancellationToken);
        if (documentReview)
            await InventoryDocumentReviewPersistence.InsertAsync(store.DbContext, source, reviewer, source.Version + 1,
                extraction.Document.SchemaDiscoveryFailure ?? "No source-backed inventory interpretation is available.",
                now, cancellationToken);
        var updated = await store.FindImportAsync(
            envelope.TenantId, importId, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(updated, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, importId, updated.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryImportExecuted,
            MasterDataReferences.CommercialEventTypes.InventoryImportExecuted, now);
    }

    private async Task<CommandOutcome> QueueDurableExtractionAsync(
        InventoryImportRow source,
        CommandEnvelope<ExecuteInventoryImportCommand> envelope,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        await extractionAttemptStore.QueueInitialAsync(
            source, envelope, provider, cancellationToken);
        await store.DbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_notify({Advertified.Commercial.Infrastructure.Worker.InventoryExtractionWakeListener.ChannelName}, {source.Id.ToString()})",
            cancellationToken);
        var queued = await store.FindImportAsync(
            envelope.TenantId, source.Id, false, cancellationToken)
            ?? throw new InvalidOperationException("The inventory import was not persisted.");
        var view = await store.BuildImportViewAsync(queued, cancellationToken);
        return OpportunityCommandSupport.Outcome(
            envelope, view, source.Id, queued.Version,
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            MasterDataReferences.CommercialActions.InventoryExtractionRequested,
            MasterDataReferences.CommercialEventTypes.InventoryExtractionRequested,
            timeProvider.GetUtcNow());
    }

    private Task<int> InsertExtractionAsync(
        TenantId tenantId,
        Guid importId,
        Guid artifactId,
        InventoryExtractionResult extraction,
        long sourceFileVersion,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash, adapter_code, adapter_version,
                schema_version, provider_json, provider_output_hash,
                canonical_json, canonical_output_hash, completed_at_utc, source_file_version)
            VALUES ({artifactId}, {tenantId.Value}, {importId}, {extraction.SourceHash},
                {extraction.AdapterCode}, {extraction.AdapterVersion},
                {extraction.SchemaVersion}, {extraction.ProviderJson}::jsonb,
                {extraction.ProviderOutputHash}, {extraction.CanonicalJson}::jsonb,
                {extraction.CanonicalOutputHash}, {timeProvider.GetUtcNow()}, {sourceFileVersion})
            """, cancellationToken);

    private async Task CompleteExecutionAsync(
        TenantId tenantId,
        Guid importId,
        long expectedVersion,
        InventorySupplierResolution supplier,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                supplier_id = {supplier.SupplierId},
                supplier_name_hint = {supplier.SupplierName},
                supplier_resolution_status_code = {supplier.Status},
                supplier_identity_evidence_json = {supplier.EvidenceJson}::jsonb,
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value} AND id = {importId}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Uploaded}
              AND version = {expectedVersion}
            """, cancellationToken);
        if (changed != 1)
        {
            throw new VersionConflictException();
        }
        await RecordStepAsync(tenantId, importId, MasterDataCodes.InventoryImportStepTypes.Extraction,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
        await RecordStepAsync(tenantId, importId, MasterDataCodes.InventoryImportStepTypes.Normalization,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
        await RecordStepAsync(tenantId, importId, MasterDataCodes.InventoryImportStepTypes.Validation,
            MasterDataCodes.LifecycleStatuses.Completed, now, cancellationToken);
    }
}
