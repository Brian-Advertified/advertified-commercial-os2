using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Runtime.CompilerServices;

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
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
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

    internal Task<List<InventoryCandidateRow>> ListCandidatesAsync(
        TenantId tenantId,
        Guid importId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryCandidateRow>($"""
            SELECT id AS "Id", import_id AS "ImportId", row_number AS "RowNumber",
                status_code AS "Status", canonical_values_json::text AS "ValuesJson",
                validation_json::text AS "ValidationJson", source_locator AS "SourceLocator",
                reviewed_by AS "ReviewedBy", version AS "Version",
                updated_at_utc AS "UpdatedAtUtc"
            FROM commercial.inventory_candidates
            WHERE tenant_id = {tenantId.Value} AND import_id = {importId}
            ORDER BY row_number, id
            """).ToListAsync(cancellationToken);

    internal Task<InventoryCandidateRow?> FindCandidateAsync(
        TenantId tenantId,
        Guid candidateId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var locking = forUpdate ? " FOR UPDATE" : string.Empty;
        var query = FormattableStringFactory.Create(
            CandidateSelect + " WHERE tenant_id = {0} AND id = {1}" + locking,
            tenantId.Value, candidateId);
        return dbContext.Database.SqlQuery<InventoryCandidateRow>(query)
            .SingleOrDefaultAsync(cancellationToken);
    }

    internal Task<List<InventoryFieldEvidenceRow>> ListEvidenceAsync(
        TenantId tenantId,
        Guid candidateId,
        CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<InventoryFieldEvidenceRow>($"""
            SELECT field_name AS "FieldName", raw_value AS "RawValue",
                normalized_value AS "NormalizedValue",
                transformation_code AS "Transformation", source_locator AS "SourceLocator",
                source_hash AS "SourceHash"
            FROM commercial.inventory_candidate_fields
            WHERE tenant_id = {tenantId.Value} AND candidate_id = {candidateId}
            ORDER BY field_name
            """).ToListAsync(cancellationToken);

    internal async Task<InventoryImportView> BuildImportViewAsync(
        InventoryImportRow row,
        CancellationToken cancellationToken)
    {
        var steps = await ListStepsAsync(new(row.TenantId), row.Id, cancellationToken);
        var candidates = await ListCandidatesAsync(new(row.TenantId), row.Id, cancellationToken);
        var views = new List<InventoryCandidateView>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var evidence = await ListEvidenceAsync(
                new(row.TenantId), candidate.Id, cancellationToken);
            views.Add(candidate.ToView(evidence));
        }
        return new InventoryImportView(
            row.Id, row.SupplierId, row.SupplierName, row.FileName, row.DeclaredMediaType,
            row.DocumentClass, row.Status, row.ScanStatus, row.SourceHash, row.SourceSize,
            row.FailureCode, steps.Select(item => new InventoryImportStepView(
                item.StepType, item.Status, item.StartedAtUtc, item.CompletedAtUtc)).ToArray(),
            views, row.Version, row.UpdatedAtUtc);
    }

    private const string ImportSelect = """
        SELECT source.id AS "Id", source.tenant_id AS "TenantId",
            source.supplier_id AS "SupplierId", supplier.name AS "SupplierName",
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
        JOIN commercial.inventory_suppliers supplier
          ON supplier.tenant_id = source.tenant_id AND supplier.id = source.supplier_id
        """;

    private const string CandidateSelect = """
        SELECT id AS "Id", import_id AS "ImportId", row_number AS "RowNumber",
            status_code AS "Status", canonical_values_json::text AS "ValuesJson",
            validation_json::text AS "ValidationJson", source_locator AS "SourceLocator",
            reviewed_by AS "ReviewedBy", version AS "Version",
            updated_at_utc AS "UpdatedAtUtc"
        FROM commercial.inventory_candidates
        """;
}
