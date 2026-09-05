using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal static class InventoryReprojectionPolicy
{
    internal const string ProviderName =
        "retained-docling-projection";
    internal const string ProviderResponseCode =
        "RETAINED_ARTIFACT_REPROJECTED";

    internal static bool CanQueueAfter(string status) =>
        status is
            MasterDataCodes.InventoryExtractionAttemptStatuses.Completed or
            MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal or
            MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut or
            MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled or
            MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired;

    internal static string ExternalTaskId(Guid attemptId) =>
        "retained-projection:" + attemptId.ToString("N");

    internal static string Checkpoint(Guid inputArtifactId) =>
        "{\"inputArtifactId\":\"" +
        inputArtifactId.ToString() +
        "\",\"state\":\"local-reprojection\"}";
}

public sealed partial class InventoryExtractionAttemptStore
{
    internal async Task QueueReprojectionAsync(
        InventoryImportRow source,
        InventoryExtractionAttemptRow? latest,
        CommandEnvelope<ReprojectInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        ValidateReprojectionReason(envelope.Command.Reason);
        var projectionVersion = CurrentProjectionVersion;
        if (source.Status !=
                MasterDataCodes.LifecycleStatuses.ReviewRequired ||
            source.Version != envelope.ExpectedVersion ||
            (latest is not null &&
             !InventoryReprojectionPolicy.CanQueueAfter(
                 latest.Status)))
        {
            throw new InvalidLifecycleTransitionException();
        }

        var artifacts = await DbContext.Database.SqlQuery<Guid>($"""
            SELECT extraction.id AS "Value"
            FROM commercial.inventory_extractions extraction
            WHERE extraction.tenant_id = {source.TenantId}
              AND extraction.import_id = {source.Id}
              AND extraction.source_hash = {source.SourceHash}
              AND extraction.adapter_code = {"docling"}
              AND NOT EXISTS (
                  SELECT 1
                  FROM commercial.inventory_extraction_projections projection
                  WHERE projection.tenant_id = extraction.tenant_id
                    AND projection.input_artifact_id = extraction.id
                    AND projection.projector_code = extraction.adapter_code
                    AND projection.projector_version = {projectionVersion})
              AND NOT EXISTS (
                  SELECT 1
                  FROM commercial.inventory_candidates candidate
                  WHERE candidate.tenant_id = extraction.tenant_id
                    AND candidate.import_id = extraction.import_id
                    AND candidate.superseded_at_utc IS NULL
                    AND (
                        candidate.status_code <>
                            {MasterDataCodes.LifecycleStatuses.ReviewRequired}
                        OR candidate.reviewed_by IS NOT NULL
                        OR EXISTS (
                            SELECT 1
                            FROM commercial.inventory_review_decisions decision
                            WHERE decision.tenant_id =
                                    candidate.tenant_id
                              AND decision.candidate_id =
                                    candidate.id)))
            """).ToListAsync(cancellationToken);
        if (artifacts.Count != 1)
            throw new InvalidLifecycleTransitionException();

        var attemptId = Guid.NewGuid();
        var now = UtcNow;
        var inserted = await DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                INSERT INTO
                    commercial.inventory_extraction_attempts (
                        id, tenant_id, import_id,
                        source_file_version, source_hash,
                        stable_submission_key,
                        provider_name, provider_version,
                        status_code, polling_checkpoint,
                        attempt_number, correlation_id,
                        command_id, requested_by,
                        input_artifact_id,
                        reconciliation_notes, version,
                        created_at_utc, updated_at_utc)
                VALUES (
                    {attemptId}, {source.TenantId},
                    {source.Id}, {latest?.SourceFileVersion ?? 1},
                    {source.SourceHash},
                    {envelope.IdempotencyKey.Value},
                    {InventoryReprojectionPolicy.ProviderName},
                    {projectionVersion},
                    {MasterDataCodes.InventoryExtractionAttemptStatuses.Pending},
                    {InventoryReprojectionPolicy.Checkpoint(artifacts[0])}::jsonb,
                    {latest?.AttemptNumber + 1 ?? 1},
                    {envelope.CorrelationId.Value},
                    {envelope.CommandId.Value},
                    {envelope.ActorId.Value},
                    {artifacts[0]},
                    {envelope.Command.Reason.Trim()},
                    1, {now}, {now})
                """, cancellationToken);
        if (inserted != 1)
            throw new InvalidOperationException(
                "Inventory reprojection was not queued.");

        var changed = await DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE commercial.inventory_imports
                SET status_code =
                        {MasterDataCodes.LifecycleStatuses.Extracting},
                    version = version + 1,
                    updated_at_utc = {now}
                WHERE tenant_id = {source.TenantId}
                  AND id = {source.Id}
                  AND status_code =
                        {MasterDataCodes.LifecycleStatuses.ReviewRequired}
                  AND version = {source.Version}
                """, cancellationToken);
        if (changed != 1)
            throw new VersionConflictException();

        await MarkReprojectionStepsAsync(
            source, now, cancellationToken);
        await SignalInventoryWorkAsync(source.Id, cancellationToken);
    }

    internal async Task<InventoryProjectionSource>
        ReadProjectionSourceAsync(
            InventoryExtractionWorkerClaim claim,
            CancellationToken cancellationToken)
    {
        await using var transaction =
            await BeginSessionAsync(claim, cancellationToken);
        var row = await DbContext.Database
            .SqlQuery<InventoryProjectionSourceRow>($"""
                SELECT extraction.id AS "InputArtifactId",
                    extraction.adapter_code AS "AdapterCode",
                    extraction.provider_json::text AS "ProviderJson",
                    extraction.canonical_json::text AS "CanonicalJson",
                    source.source_file_name AS "FileName",
                    source.declared_media_type
                        AS "DeclaredMediaType",
                    source.document_class_code AS "DocumentClass",
                    source.protected_object_key AS "ProtectedObjectKey"
                FROM commercial.inventory_extraction_attempts attempt
                JOIN commercial.inventory_extractions extraction
                  ON extraction.tenant_id = attempt.tenant_id
                 AND extraction.id =
                        attempt.input_artifact_id
                JOIN commercial.inventory_imports source
                  ON source.tenant_id = attempt.tenant_id
                 AND source.id = attempt.import_id
                WHERE attempt.tenant_id = {claim.TenantId}
                  AND attempt.id = {claim.AttemptId}
                  AND attempt.provider_name =
                        {InventoryReprojectionPolicy.ProviderName}
                  AND source.source_hash = {claim.SourceHash}
                """)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidLifecycleTransitionException();
        await transaction.CommitAsync(cancellationToken);

        if (row.AdapterCode != "docling" ||
            string.IsNullOrWhiteSpace(row.ProtectedObjectKey))
            throw new InventoryExtractionUnavailableException();
        var content = await inventoryStore.ObjectStore.ReadAsync(
            row.ProtectedObjectKey, cancellationToken);
        InventoryExtractionCompletionPolicy.VerifySource(
            content, claim.SourceHash);
        return new InventoryProjectionSource(
            row.InputArtifactId,
            new InventoryExtractionRequest(
                row.FileName, row.DeclaredMediaType,
                row.DocumentClass, claim.SourceHash, content),
            row.ProviderJson, row.CanonicalJson);
    }

    private Task<int> MarkReprojectionStepsAsync(
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_import_steps (
                id, tenant_id, import_id, step_type_code,
                status_code, outcome_json,
                started_at_utc, completed_at_utc)
            SELECT gen_random_uuid(), {source.TenantId},
                {source.Id}, step.code,
                {MasterDataCodes.LifecycleStatuses.Running},
                {"{}"}::jsonb, {now}, NULL
            FROM unnest(ARRAY[
                {MasterDataCodes.InventoryImportStepTypes.Normalization},
                {MasterDataCodes.InventoryImportStepTypes.Validation}
            ]::varchar[]) AS step(code)
            ON CONFLICT (
                tenant_id, import_id, step_type_code)
            DO UPDATE SET
                status_code = EXCLUDED.status_code,
                outcome_json = EXCLUDED.outcome_json,
                started_at_utc = EXCLUDED.started_at_utc,
                completed_at_utc = NULL
            """, cancellationToken);

    internal async Task FailReprojectionAsync(
        InventoryExtractionWorkerClaim claim,
        string nextStatus,
        string failureCode,
        string notes,
        CancellationToken cancellationToken)
    {
        InventoryExtractionAttemptStateMachine
            .EnsureTransition(
                MasterDataCodes
                    .InventoryExtractionAttemptStatuses.Running,
                nextStatus);
        await using var transaction =
            await BeginSessionAsync(claim, cancellationToken);
        var now = UtcNow;
        var changed = await DbContext.Database
            .ExecuteSqlInterpolatedAsync($"""
                UPDATE
                    commercial.inventory_extraction_attempts
                SET status_code = {nextStatus},
                    completed_at_utc = {now},
                    provider_response_code =
                        {"REPROJECTION_NOT_ACCEPTED"},
                    provider_error_code = {failureCode},
                    failure_class_collection_code =
                        {MasterDataCodes.InventoryExtractionFailureClasses.Collection},
                    failure_class_code =
                        {MasterDataCodes.InventoryExtractionFailureClasses.InvalidResult},
                    reconciliation_notes = {notes},
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
        if (changed == 1)
        {
            await DbContext.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE commercial.inventory_imports
                    SET status_code =
                            {MasterDataCodes.LifecycleStatuses.ReviewRequired},
                        failure_code = {failureCode},
                        version = version + 1,
                        updated_at_utc = {now}
                    WHERE tenant_id = {claim.TenantId}
                      AND id = {claim.ImportId}
                      AND status_code =
                            {MasterDataCodes.LifecycleStatuses.Extracting}
                    """, cancellationToken);
            await DbContext.Database
                .ExecuteSqlInterpolatedAsync($"""
                    UPDATE commercial.inventory_import_steps
                    SET status_code =
                            {MasterDataCodes.LifecycleStatuses.Failed},
                        outcome_json =
                            jsonb_build_object(
                                'failureCode',
                                {failureCode}),
                        completed_at_utc = {now}
                    WHERE tenant_id = {claim.TenantId}
                      AND import_id = {claim.ImportId}
                      AND step_type_code = ANY(ARRAY[
                          {MasterDataCodes.InventoryImportStepTypes.Normalization},
                          {MasterDataCodes.InventoryImportStepTypes.Validation}
                      ]::varchar[])
                    """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateReprojectionReason(
        string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length > 2_000)
        {
            throw new ArgumentException(
                "A reprojection reason of at most 2,000 characters is required.");
        }
    }
}

internal sealed record InventoryProjectionSource(
    Guid InputArtifactId,
    InventoryExtractionRequest Request,
    string ProviderJson,
    string CanonicalJson);

internal sealed record InventoryProjectionSourceRow
{
    public Guid InputArtifactId { get; set; }
    public string AdapterCode { get; set; } =
        string.Empty;
    public string ProviderJson { get; set; } = "{}";
    public string CanonicalJson { get; set; } = "{}";
    public string FileName { get; set; } = string.Empty;
    public string DeclaredMediaType { get; set; } =
        string.Empty;
    public string DocumentClass { get; set; } =
        string.Empty;
    public string? ProtectedObjectKey { get; set; }
}
