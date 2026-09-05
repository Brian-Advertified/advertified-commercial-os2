using System.Text.Json;

using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryCandidateBatchPersistence
{
    private const int BatchSize = 250;

    // Exception-based review: only candidates flagged by the review policy
    // become ReviewRequired with a human task; clean candidates are persisted
    // as Approved with an explicit auto-certification basis marker.
    internal static async Task PersistAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid projectionId,
        Guid? reviewer,
        DateTimeOffset now,
        IReadOnlyList<PreparedInventoryCandidate> candidates,
        CancellationToken cancellationToken,
        IReadOnlySet<Guid>? rejectedCandidateIds = null)
    {
        var retainedRejections = candidates.Where(candidate => rejectedCandidateIds?.Contains(candidate.Id) == true).ToArray();
        var eligible = candidates.Where(candidate => rejectedCandidateIds?.Contains(candidate.Id) != true).ToArray();
        var review = eligible
            .Where(InventoryCandidateReviewPolicy.RequiresReview)
            .ToArray();
        var autoCertified = eligible
            .Where(candidate => !InventoryCandidateReviewPolicy.RequiresReview(candidate))
            .Select(InventoryCandidateReviewPolicy.MarkAutoCertified)
            .ToArray();
        await PersistAsync(dbContext, tenantId, importId, projectionId,
            reviewer, now, review,
            MasterDataCodes.LifecycleStatuses.ReviewRequired,
            withTasks: reviewer.HasValue, cancellationToken);
        await PersistAsync(dbContext, tenantId, importId, projectionId,
            reviewer, now, autoCertified,
            MasterDataCodes.LifecycleStatuses.Approved,
            withTasks: false, cancellationToken);
        await PersistAsync(dbContext, tenantId, importId, projectionId,
            reviewer, now, retainedRejections, MasterDataCodes.LifecycleStatuses.Rejected,
            withTasks: false, cancellationToken);
    }

    private static async Task PersistAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid importId,
        Guid projectionId,
        Guid? reviewer,
        DateTimeOffset now,
        PreparedInventoryCandidate[] candidates,
        string statusCode,
        bool withTasks,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < candidates.Length; offset += BatchSize)
        {
            var batch = candidates.Skip(offset).Take(BatchSize).ToArray();
            var candidateJson = JsonSerializer.Serialize(
                batch.Select(ToCandidatePayload), InventoryRowMapper.StoredJson);
            var fieldJson = JsonSerializer.Serialize(
                batch.SelectMany(ToFieldPayloads), InventoryRowMapper.StoredJson);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO commercial.inventory_candidates (
                    id, tenant_id, import_id, projection_id,
                    row_number, status_code,
                    proposed_values_json, canonical_values_json, validation_json,
                    source_locator, version, created_at_utc, updated_at_utc)
                SELECT value."id", {tenantId.Value}, {importId},
                    {projectionId}, value."rowNumber",
                    {statusCode},
                    value."valuesJson"::jsonb, value."valuesJson"::jsonb,
                    value."validationJson"::jsonb, value."sourceLocator", 1, {now}, {now}
                FROM jsonb_to_recordset({candidateJson}::jsonb) AS value(
                    "id" uuid, "rowNumber" integer, "valuesJson" text,
                    "validationJson" text, "sourceLocator" text,
                    "taskId" uuid);

                INSERT INTO commercial.inventory_candidate_fields (
                    id, tenant_id, candidate_id, field_name, raw_value, normalized_value,
                    transformation_code, source_locator, source_hash,
                    evidence_basis_code, verification_state_code, required_action_code,
                    captured_at_utc, effective_on, fresh_until, extraction_method_code,
                    extraction_confidence)
                SELECT value."id", {tenantId.Value}, value."candidateId",
                    value."fieldName", value."rawValue", value."normalizedValue",
                    value."transformation", value."sourceLocator", value."sourceHash",
                    value."evidenceBasis", value."verificationState", value."requiredAction",
                    value."capturedAtUtc", value."effectiveOn", value."freshUntil",
                    value."extractionMethod", value."extractionConfidence"
                FROM jsonb_to_recordset({fieldJson}::jsonb) AS value(
                    "id" uuid, "candidateId" uuid, "fieldName" text,
                    "rawValue" text, "normalizedValue" text,
                    "transformation" text, "sourceLocator" text, "sourceHash" text,
                    "evidenceBasis" text, "verificationState" text,
                    "requiredAction" text, "capturedAtUtc" timestamptz,
                    "effectiveOn" date, "freshUntil" date,
                    "extractionMethod" text, "extractionConfidence" numeric);
                """, cancellationToken);
            if (withTasks)
            {
                await InsertReviewTasksAsync(dbContext, tenantId,
                    reviewer!.Value, candidateJson, now, cancellationToken);
            }
        }
    }

    private static Task<int> InsertReviewTasksAsync(
        GovernanceDbContext dbContext,
        TenantId tenantId,
        Guid reviewer,
        string candidateJson,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlInterpolatedAsync($"""
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
            field.SourceHash, field.EvidenceBasis, field.VerificationState,
            field.RequiredAction, field.CapturedAtUtc, field.EffectiveOn,
            field.FreshUntil, field.ExtractionMethod, field.ExtractionConfidence));

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
        string SourceHash,
        string EvidenceBasis,
        string VerificationState,
        string RequiredAction,
        DateTimeOffset CapturedAtUtc,
        DateOnly? EffectiveOn,
        DateOnly? FreshUntil,
        string ExtractionMethod,
        decimal? ExtractionConfidence);
}

internal sealed record PreparedInventoryCandidate(
    Guid Id,
    int RowNumber,
    InventoryCandidateValues Values,
    IReadOnlyList<InventoryValidationIssueView> Validation,
    string SourceLocator,
    IReadOnlyList<InventoryFieldEvidenceView> Evidence,
    Guid TaskId,
    bool HasDiscoveredSchema = false);
