using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryProjectionPersistence
{
    internal static Task<int> InsertInitialAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid artifactId,
        Guid? attemptId,
        InventoryExtractionResult extraction,
        int candidateCount,
        Guid createdBy,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        InsertAsync(
            dbContext, tenantId, importId,
            artifactId, artifactId, attemptId,
            extraction, null, candidateCount,
            createdBy, now, cancellationToken);

    internal static async Task<Guid> InsertReprojectionAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid inputArtifactId,
        Guid? attemptId,
        InventoryExtractionResult extraction,
        int candidateCount,
        Guid createdBy,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var projectionId = Guid.NewGuid();
        await InsertAsync(
            dbContext, tenantId, importId,
            projectionId, inputArtifactId, attemptId,
            extraction, extraction.CanonicalJson,
            candidateCount, createdBy, now,
            cancellationToken);
        return projectionId;
    }

    private static Task<int> InsertAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid projectionId,
        Guid inputArtifactId,
        Guid? attemptId,
        InventoryExtractionResult extraction,
        string? canonicalJson,
        int candidateCount,
        Guid createdBy,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO
                commercial.inventory_extraction_projections (
                    id, tenant_id, import_id,
                    input_artifact_id, attempt_id,
                    projector_code, projector_version,
                    schema_version, canonical_json,
                    canonical_output_hash, candidate_count,
                    created_by, created_at_utc)
            VALUES (
                {projectionId}, {tenantId.Value}, {importId},
                {inputArtifactId}, {attemptId},
                {extraction.AdapterCode},
                {extraction.AdapterVersion},
                {extraction.SchemaVersion},
                {canonicalJson}::jsonb,
                {extraction.CanonicalOutputHash},
                {candidateCount}, {createdBy}, GREATEST({now}, COALESCE((
                    SELECT MAX(created_at_utc) + INTERVAL '1 microsecond'
                    FROM commercial.inventory_extraction_projections
                    WHERE tenant_id = {tenantId.Value} AND import_id = {importId}), {now})))
            """, cancellationToken);

    internal static async Task SupersedeCurrentCandidatesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool interpretationCorrection = false)
    {
        var unsafeState = await dbContext.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1
                    FROM commercial.inventory_candidates candidate
                    WHERE candidate.tenant_id = {tenantId.Value}
                      AND candidate.import_id = {importId}
                      AND candidate.superseded_at_utc IS NULL
                      AND (
                          (NOT {interpretationCorrection} AND (candidate.status_code <>
                            {MasterDataCodes.LifecycleStatuses.ReviewRequired}
                          OR candidate.reviewed_by IS NOT NULL
                          OR EXISTS (
                              SELECT 1
                              FROM commercial.inventory_review_decisions decision
                              WHERE decision.tenant_id =
                                    candidate.tenant_id
                                AND decision.candidate_id =
                                    candidate.id)))
                          OR EXISTS (
                              SELECT 1
                              FROM commercial.inventory_product_versions version
                              WHERE version.tenant_id =
                                    candidate.tenant_id
                                AND version.source_candidate_id =
                                    candidate.id))
                ) AS "Value"
                """)
            .SingleAsync(cancellationToken);
        if (unsafeState)
            throw new InvalidLifecycleTransitionException();

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks task
            SET status_code = {MasterDataCodes.LifecycleStatuses.Cancelled},
                completed_by = {actorId},
                completed_at_utc = {now},
                completion_json =
                    {"{\"reason\":\"SUPERSEDED_BY_REPROJECTION\"}"}::jsonb,
                version = task.version + 1
            FROM commercial.inventory_candidates candidate
            WHERE candidate.tenant_id = {tenantId.Value}
              AND candidate.import_id = {importId}
              AND candidate.superseded_at_utc IS NULL
              AND task.tenant_id = candidate.tenant_id
              AND task.resource_id = candidate.id
              AND task.task_type_code =
                    {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview}
              AND task.status_code = {MasterDataCodes.LifecycleStatuses.Pending}
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_candidates
            SET superseded_at_utc = {now},
                version = version + 1,
                updated_at_utc = {now}
            WHERE tenant_id = {tenantId.Value}
              AND import_id = {importId}
              AND superseded_at_utc IS NULL
            """, cancellationToken);
    }
}
