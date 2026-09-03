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
        var reviewer = await FindReviewerAsync(
            envelope.TenantId, source.CreatedBy, cancellationToken);
        var content = await store.ObjectStore.ReadAsync(
            source.ProtectedObjectKey, cancellationToken);
        InventoryExtractionCompletionPolicy.VerifySource(content, source.SourceHash);
        var extraction = await extractionAdapter.ExtractAsync(
            new InventoryExtractionRequest(
                source.FileName, source.DeclaredMediaType, source.DocumentClass,
                source.SourceHash, content), cancellationToken);
        InventoryExtractionCompletionPolicy.VerifyResult(extraction, source.SourceHash);
        await InsertExtractionAsync(
            envelope.TenantId, source.Id, extraction, cancellationToken);
        var rows = extraction.Rows;
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var candidates = rows.Select(row => InventoryExtractionCompletionPolicy.PrepareCandidate(
            InventoryCandidateNormalizer.Normalize(row, source.SourceHash, now),
            source.SupplierName, codes)).ToArray();
        await InventoryCandidateBatchPersistence.PersistAsync(
            store.DbContext, envelope.TenantId, source.Id, reviewer, now,
            candidates, cancellationToken);
        await CompleteExecutionAsync(
            envelope.TenantId, importId, source.Version, now, cancellationToken);
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

    private async Task<Guid> FindReviewerAsync(
        TenantId tenantId,
        Guid creatorId,
        CancellationToken cancellationToken)
    {
        var reviewers = await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT membership.user_id AS "Value"
            FROM commercial.memberships membership
            WHERE membership.tenant_id = {tenantId.Value}
              AND membership.user_id <> {creatorId}
              AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND membership.role_code = ANY({InventoryReviewerRoles.Inventory})
            ORDER BY membership.role_code, membership.user_id
            LIMIT 1
            """).ToListAsync(cancellationToken);
        return reviewers.Count == 1
            ? reviewers[0] : throw new ApprovalRequiredException();
    }

    private Task<int> InsertExtractionAsync(
        TenantId tenantId,
        Guid importId,
        InventoryExtractionResult extraction,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash, adapter_code, adapter_version,
                schema_version, provider_json, provider_output_hash,
                canonical_json, canonical_output_hash, completed_at_utc)
            VALUES ({Guid.NewGuid()}, {tenantId.Value}, {importId}, {extraction.SourceHash},
                {extraction.AdapterCode}, {extraction.AdapterVersion},
                {extraction.SchemaVersion}, {extraction.ProviderJson}::jsonb,
                {extraction.ProviderOutputHash}, {extraction.CanonicalJson}::jsonb,
                {extraction.CanonicalOutputHash}, {timeProvider.GetUtcNow()})
            """, cancellationToken);

    private async Task CompleteExecutionAsync(
        TenantId tenantId,
        Guid importId,
        long expectedVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired}, version = version + 1,
                updated_at_utc = {now}
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
