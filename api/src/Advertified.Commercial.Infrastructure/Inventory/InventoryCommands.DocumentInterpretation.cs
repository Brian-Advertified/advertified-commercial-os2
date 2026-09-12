using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryCommands
{
    private async Task<CommandOutcome> ReevaluateDocumentAsync(Guid importId,
        CommandEnvelope<ReprojectInventoryExtractionCommand> envelope, CancellationToken cancellationToken)
    {
        var source = await store.FindImportAsync(envelope.TenantId, importId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Inventory import access denied.");
        if (source.Version != envelope.ExpectedVersion) throw new VersionConflictException();
        if (source.CreatedBy == envelope.ActorId.Value) throw new ApprovalRequiredException();
        if (source.Status != MasterDataCodes.LifecycleStatuses.ReviewRequired || source.PublishedReleaseId is not null ||
            source.ProtectedObjectKey is null || source.FailureCode is not (null or InventoryDocumentReviewPersistence.FailureCode))
            throw new InvalidLifecycleTransitionException();
        await EnsureDocumentReviewerAsync(source, envelope.ActorId.Value, cancellationToken);
        var artifact = await InventoryRetainedAcceptance.LoadImportAsync(store.DbContext,
            envelope.TenantId, importId, cancellationToken) ?? throw new InventoryExtractionUnavailableException();
        var retained = artifact.Extraction();
        if (retained.SourceHash != source.SourceHash || artifact.SourceFileVersion <= 0)
            throw new InventoryExtractionUnavailableException();
        if (envelope.Command.ExpectedMappingRevision != retained.CanonicalOutputHash)
            throw new VersionConflictException();
        if (string.IsNullOrWhiteSpace(envelope.Command.Reason) || envelope.Command.Reason.Length > 2000)
            throw new ArgumentException("An interpretation review reason is required.");
        var codes = await InventoryCodeSets.LoadAsync(store.DbContext, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await PersistReevaluationAsync(source, artifact, retained, envelope.ActorId.Value,
            codes, now, cancellationToken);
        return await BuildExtractionOutcomeAsync(source, envelope,
            MasterDataReferences.CommercialActions.InventoryExtractionReprojected,
            MasterDataReferences.CommercialEventTypes.InventoryExtractionReprojected, cancellationToken);
    }

    private async Task PersistReevaluationAsync(InventoryImportRow source, InventoryAcceptanceArtifact artifact,
        InventoryExtractionResult retained, Guid actorId, InventoryCodeSets codes,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tenant = new TenantId(source.TenantId);
        var candidates = InventoryCandidateAdmissionPolicy.Prepare(retained.Rows, source.SourceHash,
            source.SupplierName, codes, now);
        candidates = InventoryAcceptancePolicy.Apply(retained, source.SourceHash,
            artifact.SourceFileVersion, codes, candidates, now);
        var rejected = await InventoryRejectionCarryForward.FromHistoryAsync(store.DbContext,
            tenant, source.Id, retained, candidates, cancellationToken);
        await InventoryProjectionPersistence.SupersedeCurrentCandidatesAsync(store.DbContext,
            tenant, source.Id, actorId, now, cancellationToken, interpretationCorrection: false);
        var projectionId = retained.CanonicalOutputHash == artifact.CanonicalHash ? artifact.ProjectionId :
            await InventoryProjectionPersistence.InsertReprojectionAsync(store.DbContext,
                tenant, source.Id, artifact.ExtractionId, null, retained, candidates.Length, actorId, now, cancellationToken);
        await InventoryCandidateBatchPersistence.PersistAsync(store.DbContext, tenant, source.Id,
            projectionId, actorId, now, candidates, cancellationToken, rejected);
        var documentFailure =
            retained.Document.SchemaDiscoveryFailure is not null ||
            candidates.Length == 0;
        await CompleteDocumentInterpretationAsync(source, actorId, now, cancellationToken);
        if (documentFailure)
            await InventoryDocumentReviewPersistence.InsertAsync(store.DbContext, source, actorId, source.Version + 1,
                retained.Document.SchemaDiscoveryFailure ?? "No inventory records can be validated from the retained interpretation.",
                now, cancellationToken);
        await SetReevaluationReviewStepAsync(tenant, source.Id, documentFailure || candidates
            .Any(candidate => !rejected.Contains(candidate.Id) && InventoryCandidateReviewPolicy.RequiresReview(candidate)),
            now, cancellationToken);
    }

    private Task<int> SetReevaluationReviewStepAsync(
        TenantId tenant,
        Guid importId,
        bool needsReview,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code, status_code,
                outcome_json, started_at_utc, completed_at_utc)
            VALUES (gen_random_uuid(), {tenant.Value}, {importId}, {MasterDataCodes.InventoryImportStepTypes.Review},
                {(needsReview ? MasterDataCodes.LifecycleStatuses.ReviewRequired : MasterDataCodes.LifecycleStatuses.Completed)},
                jsonb_build_object('acceptancePolicyVersion', {InventoryAcceptancePolicy.Version}), {now},
                {(needsReview ? (DateTimeOffset?)null : now)})
            ON CONFLICT (tenant_id, import_id, step_type_code) DO UPDATE
            SET status_code = EXCLUDED.status_code,
                outcome_json = EXCLUDED.outcome_json,
                completed_at_utc = EXCLUDED.completed_at_utc
            """, cancellationToken);

    private Task<int> CompleteDocumentInterpretationAsync(InventoryImportRow source, Guid actorId,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        store.DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET version = version + 1, updated_at_utc = {now}, failure_code = NULL
            WHERE tenant_id = {source.TenantId} AND id = {source.Id};
            UPDATE commercial.human_tasks
            SET status_code = {MasterDataCodes.LifecycleStatuses.Completed}, completed_by = {actorId},
                completed_at_utc = {now}, version = version + 1,
                completion_json = jsonb_build_object('acceptancePolicyVersion', {InventoryAcceptancePolicy.Version})
            WHERE tenant_id = {source.TenantId} AND resource_id = {source.Id}
              AND resource_type_code = {MasterDataReferences.CommercialResourceTypes.InventoryImport.Value}
              AND task_type_code = {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending};
            """, cancellationToken);

    private async Task EnsureDocumentReviewerAsync(InventoryImportRow source, Guid reviewer, CancellationToken cancellationToken)
    {
        var assignees = await store.DbContext.Database.SqlQuery<Guid>($"""
            SELECT DISTINCT assignee_user_id AS "Value"
            FROM commercial.human_tasks task
            WHERE task.tenant_id = {source.TenantId}
              AND task.task_type_code = {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview}
              AND task.status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND (task.resource_id = {source.Id} OR EXISTS (
                  SELECT 1 FROM commercial.inventory_candidates candidate
                  WHERE candidate.tenant_id = task.tenant_id AND candidate.id = task.resource_id
                    AND candidate.import_id = {source.Id} AND candidate.superseded_at_utc IS NULL))
            """).ToListAsync(cancellationToken);
        if (assignees.Count > 0 && !assignees.Contains(reviewer)) throw new ApprovalRequiredException();
    }
}
