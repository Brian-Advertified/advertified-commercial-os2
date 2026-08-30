using System.Text.Json;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryCandidateBatchPersistence
{
    private const int BatchSize = 250;

    internal static async Task PersistAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid reviewer,
        DateTimeOffset now,
        IReadOnlyList<PreparedInventoryCandidate> candidates,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < candidates.Count; offset += BatchSize)
        {
            var batch = candidates.Skip(offset).Take(BatchSize).ToArray();
            var candidateJson = JsonSerializer.Serialize(
                batch.Select(ToCandidatePayload), InventoryRowMapper.StoredJson);
            var fieldJson = JsonSerializer.Serialize(
                batch.SelectMany(ToFieldPayloads), InventoryRowMapper.StoredJson);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_candidates (
                    id, tenant_id, import_id, row_number, status_code,
                    proposed_values_json, canonical_values_json, validation_json,
                    source_locator, version, created_at_utc, updated_at_utc)
                SELECT value."id", {tenantId.Value}, {importId}, value."rowNumber",
                    {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                    value."valuesJson"::jsonb, value."valuesJson"::jsonb,
                    value."validationJson"::jsonb, value."sourceLocator", 1, {now}, {now}
                FROM jsonb_to_recordset({candidateJson}::jsonb) AS value(
                    "id" uuid, "rowNumber" integer, "valuesJson" text,
                    "validationJson" text, "sourceLocator" text,
                    "taskId" uuid);

                INSERT INTO commercial.inventory_candidate_fields (
                    id, tenant_id, candidate_id, field_name, raw_value, normalized_value,
                    transformation_code, source_locator, source_hash)
                SELECT value."id", {tenantId.Value}, value."candidateId",
                    value."fieldName", value."rawValue", value."normalizedValue",
                    value."transformation", value."sourceLocator", value."sourceHash"
                FROM jsonb_to_recordset({fieldJson}::jsonb) AS value(
                    "id" uuid, "candidateId" uuid, "fieldName" text,
                    "rawValue" text, "normalizedValue" text,
                    "transformation" text, "sourceLocator" text, "sourceHash" text);

                INSERT INTO commercial.human_tasks (
                    id, tenant_id, opportunity_id, task_type_code, status_code, title,
                    why_it_matters, resource_type_code, resource_id, resource_version,
                    assignee_user_id, action_schema_json, version, created_at_utc)
                SELECT value."taskId", {tenantId.Value}, NULL,
                    {MasterDataCodes.HumanTaskTypes.InventoryCandidateReview},
                    {MasterDataCodes.LifecycleStatuses.Pending},
                    {"Review inventory candidate"},
                    {"Verify source-linked fields before inventory publication."},
                    {MasterDataReferences.CommercialResourceTypes.InventoryCandidate.Value},
                    value."id", 1, {reviewer}, {"{}"}::jsonb, 1, {now}
                FROM jsonb_to_recordset({candidateJson}::jsonb) AS value(
                    "id" uuid, "rowNumber" integer, "valuesJson" text,
                    "validationJson" text, "sourceLocator" text,
                    "taskId" uuid);
                """, cancellationToken);
        }
    }

    private static CandidatePayload ToCandidatePayload(PreparedInventoryCandidate candidate) =>
        new(
            candidate.Id,
            candidate.RowNumber,
            JsonSerializer.Serialize(candidate.Values, InventoryRowMapper.StoredJson),
            JsonSerializer.Serialize(candidate.Validation, InventoryRowMapper.StoredJson),
            candidate.SourceLocator,
            candidate.TaskId);

    private static IEnumerable<FieldPayload> ToFieldPayloads(
        PreparedInventoryCandidate candidate) =>
        candidate.Evidence.Select(field => new FieldPayload(
            Guid.NewGuid(), candidate.Id, field.FieldName, field.RawValue,
            field.NormalizedValue, field.Transformation, field.SourceLocator,
            field.SourceHash));

    private sealed record CandidatePayload(
        Guid Id,
        int RowNumber,
        string ValuesJson,
        string ValidationJson,
        string SourceLocator,
        Guid TaskId);

    private sealed record FieldPayload(
        Guid Id,
        Guid CandidateId,
        string FieldName,
        string? RawValue,
        string? NormalizedValue,
        string Transformation,
        string SourceLocator,
        string SourceHash);
}

internal sealed record PreparedInventoryCandidate(
    Guid Id,
    int RowNumber,
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryValidationIssueView> Validation,
    string SourceLocator,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    Guid TaskId);
