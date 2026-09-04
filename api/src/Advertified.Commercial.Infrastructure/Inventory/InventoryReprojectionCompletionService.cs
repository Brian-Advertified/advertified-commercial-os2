using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence.Records;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed class InventoryReprojectionCompletionService(
    InventoryExtractionAttemptStore attemptStore,
    InventoryRecordStore inventoryStore,
    TimeProvider timeProvider)
{
    internal async Task<bool> ApplyAsync(
        InventoryExtractionWorkerClaim claim,
        Guid inputArtifactId,
        InventoryExtractionResult extraction,
        CancellationToken cancellationToken)
    {
        InventoryExtractionCompletionPolicy.VerifyResult(
            extraction, claim.SourceHash);
        await using var transaction =
            await attemptStore.BeginSessionAsync(
                claim, cancellationToken);
        if (!await LockCurrentClaimAsync(
                claim, inputArtifactId,
                cancellationToken))
        {
            await transaction.RollbackAsync(
                cancellationToken);
            return false;
        }

        var tenantId = new TenantId(claim.TenantId);
        var source = await inventoryStore.FindImportAsync(
            tenantId, claim.ImportId, true,
            cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "Inventory import access denied.");
        if (source.Status !=
                MasterDataCodes.LifecycleStatuses.Extracting ||
            source.SourceHash != claim.SourceHash)
        {
            throw new InvalidLifecycleTransitionException();
        }

        var reviewer = await FindReviewerAsync(
            source, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var codes = await InventoryCodeSets.LoadAsync(
            attemptStore.DbContext, cancellationToken);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(
            extraction.Rows,
            source.SourceHash,
            source.SupplierName,
            codes,
            now);

        var projectionId =
            await InventoryProjectionPersistence
                .InsertReprojectionAsync(
                    attemptStore.DbContext, tenantId,
                    source.Id, inputArtifactId,
                    claim.AttemptId, extraction,
                    candidates.Length, claim.RequestedBy,
                    now, cancellationToken);
        await InventoryProjectionPersistence
            .SupersedeCurrentCandidatesAsync(
                attemptStore.DbContext, tenantId,
                source.Id, claim.RequestedBy, now,
                cancellationToken);
        await InventoryCandidateBatchPersistence.PersistAsync(
            attemptStore.DbContext, tenantId, source.Id,
            projectionId, reviewer, now, candidates,
            cancellationToken);

        var importVersion = await CompleteImportAsync(
            source, projectionId, now,
            cancellationToken);
        var completed = await CompleteAttemptAsync(
            claim, inputArtifactId, now,
            cancellationToken);
        if (completed != 1)
            throw new InvalidOperationException(
                "Inventory reprojection completion was fenced.");

        AddCompletionConsequences(
            claim, importVersion, inputArtifactId,
            projectionId, now);
        await attemptStore.DbContext.SaveChangesAsync(
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<bool> LockCurrentClaimAsync(
        InventoryExtractionWorkerClaim claim,
        Guid inputArtifactId,
        CancellationToken cancellationToken)
    {
        var matches = await attemptStore.DbContext.Database
            .SqlQuery<int>($"""
                SELECT 1 AS "Value"
                FROM commercial.inventory_extraction_attempts attempt
                WHERE attempt.tenant_id = {claim.TenantId}
                  AND attempt.id = {claim.AttemptId}
                  AND attempt.provider_name =
                        {InventoryReprojectionPolicy.ProviderName}
                  AND attempt.input_artifact_id =
                        {inputArtifactId}
                  AND attempt.status_code =
                        {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
                  AND attempt.worker_lease_token =
                        {claim.ClaimToken}
                  AND attempt.worker_lease_expires_at_utc >
                        {timeProvider.GetUtcNow()}
                  AND NOT EXISTS (
                      SELECT 1
                      FROM commercial.inventory_extraction_attempts newer
                      WHERE newer.tenant_id =
                            attempt.tenant_id
                        AND newer.import_id =
                            attempt.import_id
                        AND newer.attempt_number >
                            attempt.attempt_number)
                FOR UPDATE
                """)
            .ToListAsync(cancellationToken);
        return matches.Count == 1;
    }

    private async Task<Guid> FindReviewerAsync(
        InventoryImportRow source,
        CancellationToken cancellationToken)
    {
        var reviewers = await attemptStore.DbContext.Database
            .SqlQuery<Guid>($"""
                SELECT membership.user_id AS "Value"
                FROM commercial.memberships membership
                WHERE membership.tenant_id =
                        {source.TenantId}
                  AND membership.user_id <>
                        {source.CreatedBy}
                  AND membership.status_code =
                        {MasterDataCodes.LifecycleStatuses.Active}
                  AND membership.role_code =
                        ANY({InventoryReviewerRoles.Inventory})
                ORDER BY membership.role_code,
                    membership.user_id
                LIMIT 1
                """)
            .ToListAsync(cancellationToken);
        return reviewers.Count == 1
            ? reviewers[0]
            : throw new ApprovalRequiredException();
    }

    private async Task<long> CompleteImportAsync(
        InventoryImportRow source,
        Guid projectionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var versions = await attemptStore.DbContext.Database
            .SqlQuery<long>($"""
                UPDATE commercial.inventory_imports
                SET status_code =
                        {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                    failure_code = NULL,
                    version = version + 1,
                    updated_at_utc = {now}
                WHERE tenant_id = {source.TenantId}
                  AND id = {source.Id}
                  AND status_code =
                        {MasterDataCodes.LifecycleStatuses.Extracting}
                RETURNING version AS "Value"
                """)
            .ToListAsync(cancellationToken);
        if (versions.Count != 1)
            throw new InvalidLifecycleTransitionException();

        await attemptStore.DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_import_steps
                SET status_code =
                        {MasterDataCodes.LifecycleStatuses.Completed},
                    outcome_json =
                        jsonb_build_object(
                            'projectionId',
                            {projectionId}),
                    completed_at_utc = {now}
                WHERE tenant_id = {source.TenantId}
                  AND import_id = {source.Id}
                  AND step_type_code = ANY(ARRAY[
                      {MasterDataCodes.InventoryImportStepTypes.Normalization},
                      {MasterDataCodes.InventoryImportStepTypes.Validation}
                  ]::varchar[])
                """, cancellationToken);
        return versions[0];
    }

    private Task<int> CompleteAttemptAsync(
        InventoryExtractionWorkerClaim claim,
        Guid inputArtifactId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        InventoryExtractionAttemptStateMachine
            .EnsureTransition(
                MasterDataCodes
                    .InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes
                    .InventoryExtractionAttemptStatuses.Completed);
        return attemptStore.DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE
                    commercial.inventory_extraction_attempts
                SET status_code =
                        {MasterDataCodes.InventoryExtractionAttemptStatuses.Completed},
                    completed_at_utc = {now},
                    last_polled_at_utc = {now},
                    polling_checkpoint =
                        {InventoryReprojectionPolicy.Checkpoint(inputArtifactId)}::jsonb,
                    provider_response_code =
                        {InventoryReprojectionPolicy.ProviderResponseCode},
                    provider_error_code = NULL,
                    failure_class_collection_code = NULL,
                    failure_class_code = NULL,
                    extracted_artifact_id =
                        {inputArtifactId},
                    worker_id = NULL,
                    worker_lease_token = NULL,
                    worker_lease_expires_at_utc = NULL
                WHERE tenant_id = {claim.TenantId}
                  AND id = {claim.AttemptId}
                  AND status_code =
                        {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
                  AND worker_lease_token =
                        {claim.ClaimToken}
                """, cancellationToken);
    }

    private void AddCompletionConsequences(
        InventoryExtractionWorkerClaim claim,
        long version,
        Guid inputArtifactId,
        Guid projectionId,
        DateTimeOffset now)
    {
        var tenant = new TenantId(claim.TenantId);
        var resource = new ResourceReference(
            MasterDataReferences.CommercialResourceTypes
                .InventoryImport,
            claim.ImportId, version);
        attemptStore.DbContext.AuditEvents.Add(
            new AuditEventRow(new AuditRecord(
                Guid.NewGuid(), tenant,
                new ActorId(claim.RequestedBy),
                new CommandId(claim.CommandId),
                new CorrelationId(claim.CorrelationId),
                MasterDataReferences.CommercialActions
                    .InventoryExtractionReprojected,
                resource, now), "{}"));
        var payload = JsonSerializer.SerializeToElement(
            new
            {
                inventoryImportId = claim.ImportId,
                extractionAttemptId = claim.AttemptId,
                inputArtifactId,
                projectionId,
            });
        attemptStore.DbContext.OutboxMessages.Add(
            new OutboxMessageRow(new OutboxMessage(
                Guid.NewGuid(), tenant,
                new CommandId(claim.CommandId),
                new CorrelationId(claim.CorrelationId),
                MasterDataReferences.CommercialEventTypes
                    .InventoryExtractionReprojected,
                resource, payload, now)));
    }
}
