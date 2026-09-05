using System.Runtime.CompilerServices;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryRecordStore(
    GovernanceDbContext dbContext,
    IInventoryObjectStore objectStore,
    IInventoryMalwareScanner malwareScanner)
{
    internal GovernanceDbContext DbContext => dbContext;
    internal IInventoryObjectStore ObjectStore => objectStore;
    internal IInventoryMalwareScanner MalwareScanner => malwareScanner;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal Task<InventoryImportRow?> FindImportAsync(
        TenantId tenantId,
        Guid importId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE OF source" : string.Empty;
        var query = FormattableStringFactory.Create(
            ImportSelect + " WHERE source.tenant_id = {0} AND source.id = {1}" + locking,
            tenantId.Value, importId);
        return dbContext.Database.SqlQuery<InventoryImportRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<InventoryImportStepRow>> ListStepsAsync(
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryImportStepRow>($"""
            SELECT step_type_code AS "StepType", status_code AS "Status",
                started_at_utc AS "StartedAtUtc", completed_at_utc AS "CompletedAtUtc"
            FROM commercial.inventory_import_steps
            WHERE tenant_id = {tenantId.Value} AND import_id = {importId}
            ORDER BY started_at_utc, id
            """).ToListAsync(cancellationToken);

    internal Task<List<InventoryExtractionAttemptRow>> ListExtractionAttemptsAsync(
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryExtractionAttemptRow>($"""
            SELECT id AS "Id", tenant_id AS "TenantId", import_id AS "ImportId",
                source_file_version AS "SourceFileVersion", source_hash AS "SourceHash",
                stable_submission_key AS "StableSubmissionKey",
                provider_name AS "ProviderName", provider_version AS "ProviderVersion",
                status_code AS "Status", external_task_id AS "ExternalTaskId",
                submitted_at_utc AS "SubmittedAtUtc", started_at_utc AS "StartedAtUtc",
                last_polled_at_utc AS "LastPolledAtUtc",
                completed_at_utc AS "CompletedAtUtc",
                polling_checkpoint::text AS "PollingCheckpointJson",
                attempt_number AS "AttemptNumber", worker_id AS "WorkerId",
                worker_lease_expires_at_utc AS "WorkerLeaseExpiresAtUtc",
                provider_response_code AS "ProviderResponseCode",
                provider_error_code AS "ProviderErrorCode",
                failure_class_code AS "FailureClassification",
                correlation_id AS "CorrelationId",
                extracted_artifact_id AS "ExtractedArtifactId",
                reconciliation_notes AS "ReconciliationNotes", version AS "Version"
            FROM commercial.inventory_extraction_attempts
            WHERE tenant_id = {tenantId.Value} AND import_id = {importId}
            ORDER BY attempt_number DESC
            """).ToListAsync(cancellationToken);

    internal Task<List<InventoryCandidateRow>> ListCandidatesAsync(
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryCandidateRow>(
            FormattableStringFactory.Create(
                CandidateSelect +
                " WHERE tenant_id = {0} AND import_id = {1} " +
                "AND superseded_at_utc IS NULL AND soft_deleted_at_utc IS NULL ORDER BY row_number, id",
                tenantId.Value, importId))
            .ToListAsync(cancellationToken);

    internal Task<List<InventoryCandidateRow>> ListCandidatePageAsync(
        TenantId tenantId,
        Guid importId,
        InventoryCandidateCursorValue? cursor,
        int take,
        CancellationToken cancellationToken)
    {
        var suffix = cursor is null
            ? " WHERE tenant_id = {0} AND import_id = {1} " +
              "AND superseded_at_utc IS NULL AND soft_deleted_at_utc IS NULL " +
              "ORDER BY row_number, id LIMIT {2}"
            : " WHERE tenant_id = {0} AND import_id = {1} " +
              "AND superseded_at_utc IS NULL AND soft_deleted_at_utc IS NULL " +
              "AND (row_number, id) > ({2}, {3}) " +
              "ORDER BY row_number, id LIMIT {4}";
        var arguments = cursor is null
            ? new object[] { tenantId.Value, importId, take }
            : [tenantId.Value, importId, cursor.RowNumber, cursor.Id, take];
        return dbContext.Database.SqlQuery<InventoryCandidateRow>(
            FormattableStringFactory.Create(CandidateSelect + suffix, arguments))
            .ToListAsync(cancellationToken);
    }

    internal Task<InventoryCandidateRow?> FindCandidateAsync(
        TenantId tenantId,
        Guid candidateId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
        var query = FormattableStringFactory.Create(
            CandidateSelect + " WHERE tenant_id = {0} AND id = {1} " +
            "AND superseded_at_utc IS NULL AND soft_deleted_at_utc IS NULL" + locking,
            tenantId.Value, candidateId);
        return dbContext.Database.SqlQuery<InventoryCandidateRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<InventoryFieldEvidenceRow>> ListEvidenceAsync(
        TenantId tenantId,
        Guid candidateId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryFieldEvidenceRow>(
            FormattableStringFactory.Create(
                EvidenceSelect +
                " WHERE tenant_id = {0} AND candidate_id = {1} ORDER BY field_name",
                tenantId.Value, candidateId))
            .ToListAsync(cancellationToken);

    internal Task<List<InventoryFieldEvidenceRow>> ListEvidenceAsync(
        TenantId tenantId,
        Guid[] candidateIds,
        CancellationToken cancellationToken)
    {
        if (candidateIds.Length == 0)
        {
            return Task.FromResult(new List<InventoryFieldEvidenceRow>());
        }
        return dbContext.Database.SqlQuery<InventoryFieldEvidenceRow>(
            FormattableStringFactory.Create(
                EvidenceSelect +
                " WHERE tenant_id = {0} AND candidate_id = ANY({1}) " +
                "ORDER BY candidate_id, field_name",
                tenantId.Value, candidateIds))
            .ToListAsync(cancellationToken);
    }

    internal Task<InventoryCandidateCountsRow> GetCandidateCountsAsync(
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryCandidateCountsRow>($"""
            SELECT count(*)::integer AS "Total",
                count(*) FILTER (WHERE status_code =
                    {MasterDataCodes.LifecycleStatuses.ReviewRequired})::integer
                    AS "ReviewRequired",
                count(*) FILTER (WHERE status_code =
                    {MasterDataCodes.LifecycleStatuses.Approved})::integer AS "Approved",
                count(*) FILTER (WHERE status_code =
                    {MasterDataCodes.LifecycleStatuses.Rejected})::integer AS "Rejected",
                count(*) FILTER (WHERE status_code <>
                    {MasterDataCodes.LifecycleStatuses.Rejected} AND jsonb_path_exists(
                    validation_json, '$[*] ? (@.isBlocking == true)'))::integer AS "Blocking"
            FROM commercial.inventory_candidates
            WHERE tenant_id = {tenantId.Value} AND import_id = {importId}
              AND superseded_at_utc IS NULL AND soft_deleted_at_utc IS NULL
            """).SingleAsync(cancellationToken);

    internal Task<InventoryImportView> BuildImportViewAsync(
        InventoryImportRow row,
        CancellationToken cancellationToken) =>
        BuildImportViewAsync(row, 100, null, cancellationToken);

    internal async Task<InventoryImportView> BuildImportViewAsync(
        InventoryImportRow row,
        int pageSize,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
        var tenantId = new TenantId(row.TenantId);
        var decoded = InventoryCandidateCursor.Decode(cursor);
        var steps = await ListStepsAsync(tenantId, row.Id, cancellationToken);
        var attempts = await ListExtractionAttemptsAsync(
            tenantId, row.Id, cancellationToken);
        var rows = await ListCandidatePageAsync(
            tenantId, row.Id, decoded, pageSize + 1, cancellationToken);
        var selected = rows.Take(pageSize).ToArray();
        var evidence = await ListEvidenceAsync(
            tenantId, selected.Select(item => item.Id).ToArray(), cancellationToken);
        var byCandidate = evidence.ToLookup(item => item.CandidateId);
        var counts = await GetCandidateCountsAsync(tenantId, row.Id, cancellationToken);
        var interpretation = await ReadInterpretationAsync(tenantId, row.Id, cancellationToken);
        var next = rows.Count > pageSize
            ? InventoryCandidateCursor.Encode(selected[^1].RowNumber, selected[^1].Id)
            : null;
        return new InventoryImportView(
            row.Id, row.SupplierId, row.SupplierName, row.SupplierNameHint,
            row.SupplierResolutionStatus, row.SupplierIdentityEvidenceJson,
            row.ReplacementMode, row.PublishedReleaseId,
            row.FileName, row.DeclaredMediaType,
            row.DocumentClass, row.Status, row.ScanStatus, row.SourceHash, row.SourceSize,
            row.FailureCode, steps.Select(item => new InventoryImportStepView(
                item.StepType, item.Status, item.StartedAtUtc, item.CompletedAtUtc)).ToArray(),
            selected.Select(item => item.ToView(byCandidate[item.Id].ToArray())).ToArray(),
            new InventoryCandidateCountsView(
                counts.Total, counts.ReviewRequired, counts.Approved,
                counts.Rejected, counts.Blocking),
            next, attempts.Select(item => item.ToView()).ToArray(),
            row.Version, row.UpdatedAtUtc, interpretation);
    }

    private async Task<InventoryInterpretationView?> ReadInterpretationAsync(TenantId tenantId,
        Guid importId, CancellationToken cancellationToken)
    {
        var artifact = await InventoryRetainedAcceptance.LoadImportAsync(dbContext, tenantId, importId, cancellationToken);
        if (artifact is null) return null;
        var extraction = artifact.Extraction();
        var schema = extraction.Document.DiscoveredSchema;
        return new(InventoryInterpretationRevision.Revision(extraction),
            schema is null ? null : System.Text.Json.JsonSerializer.Serialize(schema, InventoryRowMapper.StoredJson),
            System.Text.Json.JsonSerializer.Serialize(
                InventoryDocumentStructureReader.Read(extraction.SourceHash, extraction.ProviderJson), InventoryRowMapper.StoredJson),
            extraction.Document.SchemaDiscoveryFailure);
    }

    private const string ImportSelect = """
        SELECT source.id AS "Id", source.tenant_id AS "TenantId",
            source.supplier_id AS "SupplierId",
            COALESCE(supplier.name, NULLIF(source.supplier_name_hint, ''),
                'Supplier to be identified') AS "SupplierName",
            source.supplier_name_hint AS "SupplierNameHint",
            source.supplier_resolution_status_code AS "SupplierResolutionStatus",
            source.supplier_identity_evidence_json::text AS "SupplierIdentityEvidenceJson",
            source.replacement_mode_code AS "ReplacementMode",
            source.published_release_id AS "PublishedReleaseId",
            source.source_file_name AS "FileName",
            source.declared_media_type AS "DeclaredMediaType",
            source.document_class_code AS "DocumentClass", source.status_code AS "Status",
            source.scan_status_code AS "ScanStatus",
            source.quarantine_object_key AS "QuarantineObjectKey",
            source.protected_object_key AS "ProtectedObjectKey",
            source.source_hash AS "SourceHash", source.source_size AS "SourceSize",
            source.failure_code AS "FailureCode", source.created_by AS "CreatedBy",
            source.version AS "Version", source.updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.inventory_imports source
        LEFT JOIN commercial.inventory_suppliers supplier
          ON supplier.tenant_id = source.tenant_id AND supplier.id = source.supplier_id
        """;

    private const string CandidateSelect = """
        SELECT id AS "Id", projection_id AS "ProjectionId", import_id AS "ImportId", row_number AS "RowNumber",
            status_code AS "Status", canonical_values_json::text AS "ValuesJson",
            validation_json::text AS "ValidationJson", source_locator AS "SourceLocator",
            reviewed_by AS "ReviewedBy", version AS "Version",
            updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.inventory_candidates
        """;

    private const string EvidenceSelect = """
        SELECT candidate_id AS "CandidateId", field_name AS "FieldName",
            raw_value AS "RawValue", normalized_value AS "NormalizedValue",
            transformation_code AS "Transformation", source_locator AS "SourceLocator",
            source_hash AS "SourceHash", evidence_basis_code AS "EvidenceBasis",
            verification_state_code AS "VerificationState",
            required_action_code AS "RequiredAction",
            captured_at_utc AS "CapturedAtUtc", effective_on AS "EffectiveOn",
            fresh_until AS "FreshUntil", extraction_method_code AS "ExtractionMethod",
            extraction_confidence AS "ExtractionConfidence"
        FROM commercial.inventory_candidate_fields
        """;
}
