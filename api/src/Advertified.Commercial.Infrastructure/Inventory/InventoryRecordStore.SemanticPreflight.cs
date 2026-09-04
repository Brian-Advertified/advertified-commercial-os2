using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryRecordStore
{
    private const string SemanticPreflightSourcesSql = """
        SELECT DISTINCT ON (source.id)
            source.id AS "ImportId",
            extraction.id AS "InputArtifactId",
            source.source_file_name AS "FileName",
            source.declared_media_type AS "MediaType",
            source.document_class_code AS "DocumentClass",
            source.source_hash AS "SourceHash",
            source.protected_object_key AS "ProtectedObjectKey",
            source.status_code AS "ImportStatus",
            extraction.provider_json::text AS "ProviderJson",
            (
                source.status_code = {2}
                AND NOT EXISTS (
                    SELECT 1
                    FROM commercial.inventory_candidates candidate
                    WHERE candidate.tenant_id = source.tenant_id
                      AND candidate.import_id = source.id
                      AND candidate.superseded_at_utc IS NULL
                      AND (
                          candidate.status_code <> {2}
                          OR candidate.reviewed_by IS NOT NULL
                          OR EXISTS (
                              SELECT 1
                              FROM commercial.inventory_review_decisions decision
                              WHERE decision.tenant_id = candidate.tenant_id
                                AND decision.candidate_id = candidate.id)
                          OR EXISTS (
                              SELECT 1
                              FROM commercial.inventory_product_versions product_version
                              WHERE product_version.tenant_id = candidate.tenant_id
                                AND product_version.source_candidate_id = candidate.id)))
                AND NOT EXISTS (
                    SELECT 1
                    FROM commercial.inventory_extraction_projections projection
                    WHERE projection.tenant_id = extraction.tenant_id
                      AND projection.input_artifact_id = extraction.id
                      AND projection.projector_code = {3}
                      AND projection.projector_version = {4})
            ) AS "SafeToReproject"
        FROM commercial.inventory_imports source
        JOIN commercial.inventory_extractions extraction
          ON extraction.tenant_id = source.tenant_id
         AND extraction.import_id = source.id
         AND extraction.source_hash = source.source_hash
         AND extraction.adapter_code = {3}
        WHERE source.tenant_id = {0}
          AND ({1}::uuid IS NULL OR source.id = {1})
          AND (
              EXISTS (
                  SELECT 1
                  FROM commercial.inventory_extraction_attempts attempt
                  WHERE attempt.tenant_id = extraction.tenant_id
                    AND attempt.extracted_artifact_id = extraction.id
                    AND attempt.status_code = {5})
              OR NOT EXISTS (
                  SELECT 1
                  FROM commercial.inventory_extraction_attempts attempt
                  WHERE attempt.tenant_id = extraction.tenant_id
                    AND attempt.import_id = extraction.import_id))
        ORDER BY source.id, extraction.completed_at_utc DESC
        """;

    private const string SemanticPreflightRunsSql = """
        SELECT input_hash AS "InputHash",
            status_code AS "Status",
            maximum_cost_usd_micros AS "MaximumCostUsdMicros",
            incremental_cost_usd_micros AS "ActualCostUsdMicros"
        FROM commercial.inventory_semantic_runs
        WHERE tenant_id = {0}
          AND input_hash = ANY({1})
          AND model_code = {2}
          AND prompt_version = {3}
        """;

    private const string SemanticCommittedCostSql = """
        SELECT COALESCE(sum(
            CASE
                WHEN status_code = {1}
                THEN incremental_cost_usd_micros
                ELSE maximum_cost_usd_micros
            END), 0)::bigint AS "Value"
        FROM commercial.inventory_semantic_runs
        WHERE tenant_id = {0}
          AND budget_scope = {2}
        """;

    internal Task<List<SemanticPreflightSourceRow>>
        ListSemanticPreflightSourcesAsync(
            TenantId tenantId,
            Guid? importId,
            string projectionVersion,
            CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<SemanticPreflightSourceRow>(
                FormattableStringFactory.Create(
                    SemanticPreflightSourcesSql,
                    tenantId.Value,
                    importId,
                    MasterDataCodes.LifecycleStatuses.ReviewRequired,
                    "docling",
                    projectionVersion,
                    MasterDataCodes.InventoryExtractionAttemptStatuses
                        .Completed))
            .ToListAsync(cancellationToken);

    internal Task<List<SemanticPreflightRunRow>>
        ListSemanticPreflightRunsAsync(
            TenantId tenantId,
            string[] hashes,
            string model,
            string promptVersion,
            CancellationToken cancellationToken) =>
        dbContext.Database
            .SqlQuery<SemanticPreflightRunRow>(
                FormattableStringFactory.Create(
                    SemanticPreflightRunsSql,
                    tenantId.Value,
                    hashes,
                    model,
                    promptVersion))
            .ToListAsync(cancellationToken);

    internal Task<long> ReadSemanticCommittedCostAsync(
        TenantId tenantId,
        string budgetScope,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<long>(
                FormattableStringFactory.Create(
                    SemanticCommittedCostSql,
                    tenantId.Value,
                    MasterDataCodes.LifecycleStatuses.Completed,
                    budgetScope))
            .SingleAsync(cancellationToken);
}
