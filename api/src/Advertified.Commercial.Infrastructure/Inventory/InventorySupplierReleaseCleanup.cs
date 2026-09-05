using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static partial class InventorySupplierReleasePublication
{
    private static readonly string[] SupersededImportStatuses =
    [
        MasterDataCodes.LifecycleStatuses.Uploaded,
        MasterDataCodes.LifecycleStatuses.Extracting,
        MasterDataCodes.LifecycleStatuses.Validating,
        MasterDataCodes.LifecycleStatuses.ReviewRequired,
        MasterDataCodes.LifecycleStatuses.Publishing,
        MasterDataCodes.LifecycleStatuses.Failed,
    ];

    private static readonly string[] SupersededAttemptStatuses =
    [
        MasterDataCodes.InventoryExtractionAttemptStatuses.Pending,
        MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
        MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
        MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable,
        MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
        MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut,
        MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
    ];

    private static async Task SupersedeEarlierPendingWorkAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await CancelEarlierAttemptsAsync(
            dbContext, tenantId, supplierId, importId, now, cancellationToken);
        await CancelEarlierReviewTasksAsync(
            dbContext, tenantId, supplierId, importId, actorId, now,
            cancellationToken);
        await SoftDeleteEarlierCandidatesAsync(
            dbContext, tenantId, supplierId, importId, now, cancellationToken);
        await SoftDeleteEarlierImportsAsync(
            dbContext, tenantId, supplierId, importId, now, cancellationToken);
    }

    private static Task<int> CancelEarlierAttemptsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_extraction_attempts attempt
            SET status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled},
                completed_at_utc = COALESCE(attempt.completed_at_utc, {now}),
                worker_id = NULL, worker_lease_token = NULL,
                worker_lease_expires_at_utc = NULL,
                failure_class_collection_code =
                    {MasterDataCodes.InventoryExtractionFailureClasses.Collection},
                failure_class_code =
                    {MasterDataCodes.InventoryExtractionFailureClasses.CancelledByOperator},
                provider_error_code = {"SUPERSEDED_BY_NEW_RELEASE"},
                reconciliation_notes = concat_ws(E'\n', attempt.reconciliation_notes,
                    {"Superseded by a newer published supplier inventory release."}),
                version = attempt.version + 1, updated_at_utc = {now}
            WHERE attempt.tenant_id = {tenantId.Value}
              AND attempt.status_code = ANY({SupersededAttemptStatuses})
              AND attempt.import_id IN (
                  SELECT previous.id
                  FROM commercial.inventory_imports previous
                  JOIN commercial.inventory_imports current
                    ON current.tenant_id = previous.tenant_id
                   AND current.id = {importId}
                  WHERE previous.tenant_id = {tenantId.Value}
                    AND previous.supplier_id = {supplierId}
                    AND (previous.created_at_utc, previous.id) <
                        (current.created_at_utc, current.id)
                    AND previous.status_code = ANY({SupersededImportStatuses}))
            """, cancellationToken);

    private static Task<int> CancelEarlierReviewTasksAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.human_tasks task
            SET status_code = {MasterDataCodes.LifecycleStatuses.Cancelled},
                completed_by = {actorId}, completed_at_utc = {now},
                completion_json =
                    {"{\"reason\":\"supplier_inventory_replaced\"}"}::jsonb,
                version = version + 1
            WHERE task.tenant_id = {tenantId.Value}
              AND task.status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND task.resource_type_code =
                    {MasterDataReferences.CommercialResourceTypes.InventoryCandidate.Value}
              AND task.resource_id IN (
                  SELECT candidate.id
                  FROM commercial.inventory_candidates candidate
                  JOIN commercial.inventory_imports previous
                    ON previous.tenant_id = candidate.tenant_id
                   AND previous.id = candidate.import_id
                  JOIN commercial.inventory_imports current
                    ON current.tenant_id = previous.tenant_id
                   AND current.id = {importId}
                  WHERE previous.tenant_id = {tenantId.Value}
                    AND previous.supplier_id = {supplierId}
                    AND (previous.created_at_utc, previous.id) <
                        (current.created_at_utc, current.id)
                    AND previous.status_code = ANY({SupersededImportStatuses}))
            """, cancellationToken);

    private static Task<int> SoftDeleteEarlierCandidatesAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_candidates candidate
            SET status_code = {MasterDataCodes.LifecycleStatuses.Cancelled},
                superseded_at_utc = COALESCE(candidate.superseded_at_utc, {now}),
                soft_deleted_at_utc = COALESCE(candidate.soft_deleted_at_utc, {now}),
                version = candidate.version + 1, updated_at_utc = {now}
            FROM commercial.inventory_imports previous,
                 commercial.inventory_imports current
            WHERE candidate.tenant_id = {tenantId.Value}
              AND previous.tenant_id = candidate.tenant_id
              AND previous.id = candidate.import_id
              AND current.tenant_id = previous.tenant_id
              AND current.id = {importId}
              AND previous.supplier_id = {supplierId}
              AND (previous.created_at_utc, previous.id) <
                  (current.created_at_utc, current.id)
              AND previous.status_code = ANY({SupersededImportStatuses})
              AND candidate.soft_deleted_at_utc IS NULL
            """, cancellationToken);

    private static Task<int> SoftDeleteEarlierImportsAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid supplierId,
        Guid importId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports previous
            SET status_code = {MasterDataCodes.LifecycleStatuses.Cancelled},
                superseded_by_import_id = {importId},
                superseded_at_utc = COALESCE(previous.superseded_at_utc, {now}),
                soft_deleted_at_utc = COALESCE(previous.soft_deleted_at_utc, {now}),
                failure_code = COALESCE(
                    previous.failure_code, {"SUPERSEDED_BY_NEW_RELEASE"}),
                version = previous.version + 1, updated_at_utc = {now}
            FROM commercial.inventory_imports current
            WHERE previous.tenant_id = {tenantId.Value}
              AND current.tenant_id = previous.tenant_id
              AND current.id = {importId}
              AND previous.supplier_id = {supplierId}
              AND (previous.created_at_utc, previous.id) <
                  (current.created_at_utc, current.id)
              AND previous.status_code = ANY({SupersededImportStatuses})
              AND previous.soft_deleted_at_utc IS NULL
            """, cancellationToken);
}
