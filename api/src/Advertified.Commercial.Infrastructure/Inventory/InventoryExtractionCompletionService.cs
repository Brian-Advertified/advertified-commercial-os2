using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryExtractionCompletionService(
    InventoryExtractionAttemptStore attemptStore,
    InventoryRecordStore inventoryStore,
    InventorySemanticEnrichmentService semanticEnrichment,
    TimeProvider timeProvider)
{
    public async Task<bool> ApplyAsync(
        InventoryExtractionWorkerClaim claim,
        InventoryExtractionResult extraction,
        InventoryExtractionPollResult poll,
        CancellationToken cancellationToken)
    {
        InventoryExtractionCompletionPolicy.VerifyResult(
            extraction, claim.SourceHash);
        extraction = await semanticEnrichment.EnrichAsync(
            claim, extraction, cancellationToken);
        InventoryExtractionCompletionPolicy.VerifyResult(
            extraction, claim.SourceHash);
        await using var transaction = await attemptStore.BeginSessionAsync(
            claim, cancellationToken);
        if (!await LockCurrentClaimAsync(claim, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        var source = await inventoryStore.FindImportAsync(
            new TenantId(claim.TenantId), claim.ImportId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Status != MasterDataCodes.LifecycleStatuses.Extracting ||
            !string.Equals(source.SourceHash, claim.SourceHash, StringComparison.Ordinal))
        {
            throw new InvalidLifecycleTransitionException();
        }
        var reviewer = await FindReviewerAsync(source, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var codes = await InventoryCodeSets.LoadAsync(
            attemptStore.DbContext, cancellationToken);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            extraction.Rows,
            source.SourceHash,
            source.SupplierName,
            codes,
            now);
        var artifactId = Guid.NewGuid();
        var tenantId = new TenantId(claim.TenantId);
        await InsertArtifactAsync(
            claim, artifactId, extraction, now, cancellationToken);
        await InventoryProjectionPersistence.InsertInitialAsync(
            attemptStore.DbContext, tenantId, source.Id,
            artifactId, claim.AttemptId, extraction,
            candidates.Length, claim.RequestedBy, now,
            cancellationToken);
        await InventoryCandidateBatchPersistence.PersistAsync(
            attemptStore.DbContext, tenantId, source.Id,
            artifactId, reviewer, now, candidates,
            cancellationToken);
        var importVersion = await CompleteImportAsync(
            source, now, cancellationToken);
        var completed = await CompleteAttemptAsync(
            claim, artifactId, poll, now, cancellationToken);
        if (completed != 1)
        {
            throw new InvalidOperationException("Extraction completion was fenced.");
        }
        AddCompletionConsequences(claim, importVersion, artifactId, now);
        await attemptStore.DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> LockCurrentClaimAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        var matches = await attemptStore.DbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS "Value"
            FROM commercial.inventory_extraction_attempts attempt
            WHERE attempt.tenant_id = {claim.TenantId} AND attempt.id = {claim.AttemptId}
              AND attempt.status_code =
                  {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
              AND attempt.worker_lease_token = {claim.ClaimToken}
              AND attempt.worker_lease_expires_at_utc > {timeProvider.GetUtcNow()}
              AND NOT EXISTS (
                  SELECT 1 FROM commercial.inventory_extraction_attempts newer
                  WHERE newer.tenant_id = attempt.tenant_id
                    AND newer.import_id = attempt.import_id
                    AND newer.attempt_number > attempt.attempt_number)
            FOR UPDATE
            """).ToListAsync(cancellationToken);
        return matches.Count == 1;
    }

    private async Task<Guid> FindReviewerAsync(
        InventoryImportRow source,
        CancellationToken cancellationToken)
    {
        var reviewers = await attemptStore.DbContext.Database.SqlQuery<Guid>($"""
            SELECT membership.user_id AS "Value"
            FROM commercial.memberships membership
            WHERE membership.tenant_id = {source.TenantId}
              AND membership.user_id <> {source.CreatedBy}
              AND membership.status_code = {MasterDataCodes.LifecycleStatuses.Active}
              AND membership.role_code = ANY({InventoryReviewerRoles.Inventory})
            ORDER BY membership.role_code, membership.user_id LIMIT 1
            """).ToListAsync(cancellationToken);
        return reviewers.Count == 1
            ? reviewers[0]
            : throw new ApprovalRequiredException();
    }

    private Task<int> InsertArtifactAsync(
        InventoryExtractionWorkerClaim claim,
        Guid artifactId,
        InventoryExtractionResult extraction,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        attemptStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extractions (
                id, tenant_id, import_id, source_hash, adapter_code, adapter_version,
                schema_version, provider_json, provider_output_hash, canonical_json,
                canonical_output_hash, completed_at_utc, attempt_id, source_file_version)
            VALUES ({artifactId}, {claim.TenantId}, {claim.ImportId}, {extraction.SourceHash},
                {extraction.AdapterCode}, {extraction.AdapterVersion}, {extraction.SchemaVersion},
                {extraction.ProviderJson}::jsonb, {extraction.ProviderOutputHash},
                {extraction.CanonicalJson}::jsonb, {extraction.CanonicalOutputHash}, {now},
                {claim.AttemptId}, {claim.SourceFileVersion})
            """, cancellationToken);

    private async Task<long> CompleteImportAsync(
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var versions = await attemptStore.DbContext.Database.SqlQuery<long>($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {source.TenantId} AND id = {source.Id}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Extracting}
            RETURNING version AS "Value"
            """).ToListAsync(cancellationToken);
        if (versions.Count != 1)
        {
            throw new InvalidLifecycleTransitionException();
        }
        await CompleteStepsAsync(source, now, cancellationToken);
        return versions[0];
    }

    private Task<int> CompleteStepsAsync(
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        attemptStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code, status_code,
                outcome_json, started_at_utc, completed_at_utc)
            SELECT gen_random_uuid(), {source.TenantId}, {source.Id}, step.code,
                {MasterDataCodes.LifecycleStatuses.Completed}, {"{}"}::jsonb, {now}, {now}
            FROM unnest(ARRAY[
                {MasterDataCodes.InventoryImportStepTypes.Extraction},
                {MasterDataCodes.InventoryImportStepTypes.Normalization},
                {MasterDataCodes.InventoryImportStepTypes.Validation}]::varchar[]) AS step(code)
            ON CONFLICT (tenant_id, import_id, step_type_code) DO UPDATE
            SET status_code = EXCLUDED.status_code,
                completed_at_utc = EXCLUDED.completed_at_utc
            """, cancellationToken);

    private Task<int> CompleteAttemptAsync(
        InventoryExtractionWorkerClaim claim,
        Guid artifactId,
        InventoryExtractionPollResult poll,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        InventoryExtractionAttemptStateMachine.EnsureTransition(
            MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
            MasterDataCodes.InventoryExtractionAttemptStatuses.Completed);
        return attemptStore.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Completed},
                completed_at_utc = {now}, last_polled_at_utc = {now},
                polling_checkpoint = {poll.PollingCheckpointJson}::jsonb,
                provider_response_code = {poll.ProviderResponseCode},
                provider_error_code = NULL, failure_class_collection_code = NULL,
                failure_class_code = NULL, extracted_artifact_id = {artifactId},
                worker_id = NULL, worker_lease_token = NULL,
                worker_lease_expires_at_utc = NULL
            WHERE tenant_id = {claim.TenantId} AND id = {claim.AttemptId}
              AND status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
              AND worker_lease_token = {claim.ClaimToken}
            """, cancellationToken);
    }

    private void AddCompletionConsequences(
        InventoryExtractionWorkerClaim claim,
        long version,
        Guid artifactId,
        DateTimeOffset now)
    {
        var tenant = new TenantId(claim.TenantId);
        var resource = new ResourceReference(
            MasterDataReferences.CommercialResourceTypes.InventoryImport,
            claim.ImportId, version);
        attemptStore.DbContext.AuditEvents.Add(new AuditEventRow(new AuditRecord(
            Guid.NewGuid(), tenant, new ActorId(claim.RequestedBy),
            new CommandId(claim.CommandId), new CorrelationId(claim.CorrelationId),
            MasterDataReferences.CommercialActions.InventoryImportExecuted,
            resource, now), "{}"));
        var payload = JsonSerializer.SerializeToElement(new
        {
            inventoryImportId = claim.ImportId,
            extractionAttemptId = claim.AttemptId,
            extractedArtifactId = artifactId,
        });
        attemptStore.DbContext.OutboxMessages.Add(new OutboxMessageRow(new OutboxMessage(
            Guid.NewGuid(), tenant, new CommandId(claim.CommandId),
            new CorrelationId(claim.CorrelationId),
            MasterDataReferences.CommercialEventTypes.InventoryImportExecuted,
            resource, payload, now)));
    }
}
