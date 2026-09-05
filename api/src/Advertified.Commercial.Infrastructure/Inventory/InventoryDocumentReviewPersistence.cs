using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryDocumentReviewPersistence
{
    internal const string FailureCode = "SCHEMA_INTERPRETATION_REQUIRED";
    internal static async Task InsertAsync(GovernanceDbContext dbContext, InventoryImportRow source,
        Guid? reviewer, long importVersion, string failure, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await MarkUninterpretedAsync(dbContext, source, failure, now, cancellationToken);
        if (!reviewer.HasValue) return; // Retain pending evidence until an independent reviewer is available.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.human_tasks (
                id, tenant_id, opportunity_id, task_type_code, status_code, title,
                why_it_matters, resource_type_code, resource_id, resource_version,
                assignee_user_id, action_schema_json, version, created_at_utc)
            VALUES (gen_random_uuid(), {source.TenantId}, NULL,
                {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview},
                {MasterDataCodes.LifecycleStatuses.Pending}, {"Review inventory document"},
                {failure}, {MasterDataReferences.CommercialResourceTypes.InventoryImport.Value},
                {source.Id}, {importVersion}, {reviewer}, {"{}"}::jsonb, 1, {now})
            """, cancellationToken);
    }

    private static Task<int> MarkUninterpretedAsync(GovernanceDbContext dbContext,
        InventoryImportRow source, string failure, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET failure_code = {FailureCode}
            WHERE tenant_id = {source.TenantId} AND id = {source.Id};

            UPDATE commercial.inventory_import_steps
            SET status_code = {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                outcome_json = jsonb_build_object('reviewReason', {failure}),
                completed_at_utc = {now}
            WHERE tenant_id = {source.TenantId} AND import_id = {source.Id}
              AND step_type_code = ANY(ARRAY[
                  {MasterDataCodes.InventoryImportStepTypes.Normalization},
                  {MasterDataCodes.InventoryImportStepTypes.Validation}]::varchar[])
            """, cancellationToken);
}
