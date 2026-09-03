using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryExtractionAttemptStore
{
    private static readonly string[] RetryableTerminalStatuses =
    [
        MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
        MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut,
        MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled,
    ];

    internal async Task QueueRetryAsync(
        InventoryImportRow source,
        InventoryExtractionAttemptRow latest,
        CommandEnvelope<RetryInventoryExtractionCommand> envelope,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        ValidateReason(envelope.Command.Reason);
        if (source.Status != MasterDataCodes.LifecycleStatuses.Extracting ||
            source.Version != envelope.ExpectedVersion ||
            !RetryableTerminalStatuses.Contains(latest.Status, StringComparer.Ordinal))
        {
            throw new InvalidLifecycleTransitionException();
        }
        var now = UtcNow;
        var inserted = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extraction_attempts (
                id, tenant_id, import_id, source_file_version, source_hash,
                stable_submission_key, provider_name, provider_version, status_code,
                polling_checkpoint, attempt_number, correlation_id, command_id,
                requested_by, reconciliation_notes, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {source.TenantId}, {source.Id},
                {latest.SourceFileVersion}, {latest.SourceHash},
                {envelope.IdempotencyKey.Value}, {provider.ProviderName},
                {provider.ProviderVersion},
                {MasterDataCodes.InventoryExtractionAttemptStatuses.Pending},
                {"{}"}::jsonb, {latest.AttemptNumber + 1}, {envelope.CorrelationId.Value},
                {envelope.CommandId.Value}, {envelope.ActorId.Value},
                {envelope.Command.Reason.Trim()}, 1, {now}, {now})
            """, cancellationToken);
        if (inserted != 1) throw new InvalidOperationException("Extraction retry was not queued.");
        await IncrementImportVersionAsync(source, now, cancellationToken);
    }

    internal async Task CancelAsync(
        InventoryImportRow source,
        InventoryExtractionAttemptRow latest,
        CommandEnvelope<CancelInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        ValidateReason(envelope.Command.Reason);
        if (source.Version != envelope.ExpectedVersion)
            throw new VersionConflictException();
        InventoryExtractionAttemptStateMachine.EnsureTransition(
            latest.Status, MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled);
        var now = UtcNow;
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Cancelled},
                completed_at_utc = {now}, provider_response_code = {"OPERATOR_CANCELLED"},
                provider_error_code = NULL, failure_class_collection_code =
                    {MasterDataCodes.InventoryExtractionFailureClasses.Collection},
                failure_class_code =
                    {MasterDataCodes.InventoryExtractionFailureClasses.CancelledByOperator},
                reconciliation_notes = {envelope.Command.Reason.Trim()},
                worker_id = NULL, worker_lease_token = NULL,
                worker_lease_expires_at_utc = NULL
            WHERE tenant_id = {source.TenantId} AND id = {latest.Id}
              AND status_code = {latest.Status} AND version = {latest.Version}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
        await IncrementImportVersionAsync(source, now, cancellationToken);
    }

    internal async Task ReconcileAsync(
        InventoryImportRow source,
        InventoryExtractionAttemptRow? latest,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        ValidateReason(envelope.Command.Reason);
        ValidateExternalTaskId(envelope.Command.ExternalTaskId);
        if (source.Version != envelope.ExpectedVersion)
            throw new VersionConflictException();
        if (latest is null)
            await InsertReconciliationAsync(source, envelope, provider, cancellationToken);
        else
            await UpdateReconciliationAsync(source, latest, envelope, cancellationToken);
        await IncrementImportVersionAsync(source, UtcNow, cancellationToken, allowUploaded: true);
    }

    private async Task InsertReconciliationAsync(
        InventoryImportRow source,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        if (source.Status is not (MasterDataCodes.LifecycleStatuses.Uploaded or
            MasterDataCodes.LifecycleStatuses.Extracting))
            throw new InvalidLifecycleTransitionException();
        var externalTaskId = NormalizeTaskId(envelope.Command.ExternalTaskId);
        var status = externalTaskId is null
            ? MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired
            : MasterDataCodes.InventoryExtractionAttemptStatuses.Running;
        var now = UtcNow;
        DateTimeOffset? completedAt = externalTaskId is null ? now : null;
        var failureCollection = externalTaskId is null
            ? MasterDataCodes.InventoryExtractionFailureClasses.Collection : null;
        var failureClass = externalTaskId is null
            ? MasterDataCodes.InventoryExtractionFailureClasses.AmbiguousSubmission : null;
        await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.inventory_extraction_attempts (
                id, tenant_id, import_id, source_file_version, source_hash,
                stable_submission_key, provider_name, provider_version, status_code,
                external_task_id, submitted_at_utc, completed_at_utc, polling_checkpoint,
                attempt_number, provider_response_code, failure_class_collection_code,
                failure_class_code, correlation_id, command_id, requested_by,
                reconciliation_notes, version, created_at_utc, updated_at_utc)
            VALUES ({Guid.NewGuid()}, {source.TenantId}, {source.Id}, {source.Version},
                {source.SourceHash}, {envelope.IdempotencyKey.Value}, {provider.ProviderName},
                {provider.ProviderVersion}, {status}, {externalTaskId}, {now},
                {completedAt}, {"{}"}::jsonb, 1,
                {"OPERATOR_RECONCILIATION"},
                {failureCollection}, {failureClass},
                {envelope.CorrelationId.Value}, {envelope.CommandId.Value},
                {envelope.ActorId.Value}, {envelope.Command.Reason.Trim()}, 1, {now}, {now})
            """, cancellationToken);
    }

    private async Task UpdateReconciliationAsync(
        InventoryImportRow source,
        InventoryExtractionAttemptRow latest,
        CommandEnvelope<ReconcileInventoryExtractionCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (latest.Status != MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired)
            throw new InvalidLifecycleTransitionException();
        var externalTaskId = NormalizeTaskId(envelope.Command.ExternalTaskId);
        var next = externalTaskId is null ? latest.Status :
            MasterDataCodes.InventoryExtractionAttemptStatuses.Running;
        if (latest.ExternalTaskId is not null && externalTaskId is not null &&
            !string.Equals(latest.ExternalTaskId, externalTaskId, StringComparison.Ordinal))
            throw new InvalidLifecycleTransitionException();
        InventoryExtractionAttemptStateMachine.EnsureTransition(latest.Status, next);
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_extraction_attempts
            SET status_code = {next}, external_task_id = COALESCE(external_task_id, {externalTaskId}),
                completed_at_utc = CASE WHEN {externalTaskId} IS NULL
                    THEN completed_at_utc ELSE NULL END,
                failure_class_collection_code = CASE WHEN {externalTaskId} IS NULL
                    THEN failure_class_collection_code ELSE NULL END,
                failure_class_code = CASE WHEN {externalTaskId} IS NULL
                    THEN failure_class_code ELSE NULL END,
                reconciliation_notes = {envelope.Command.Reason.Trim()},
                worker_id = NULL, worker_lease_token = NULL,
                worker_lease_expires_at_utc = NULL
            WHERE tenant_id = {source.TenantId} AND id = {latest.Id}
              AND status_code = {latest.Status} AND version = {latest.Version}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    private async Task IncrementImportVersionAsync(
        InventoryImportRow source,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool allowUploaded = false)
    {
        var statuses = allowUploaded
            ? new[] { MasterDataCodes.LifecycleStatuses.Uploaded,
                MasterDataCodes.LifecycleStatuses.Extracting }
            : new[] { MasterDataCodes.LifecycleStatuses.Extracting };
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_imports
            SET status_code = {MasterDataCodes.LifecycleStatuses.Extracting},
                version = version + 1, updated_at_utc = {now}
            WHERE tenant_id = {source.TenantId} AND id = {source.Id}
              AND version = {source.Version} AND status_code = ANY({statuses})
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.");
    }

    private static void ValidateExternalTaskId(string? externalTaskId)
    {
        if (externalTaskId?.Trim().Length > 300)
            throw new ArgumentException("The external task identifier is too long.");
    }

    private static string? NormalizeTaskId(string? externalTaskId) =>
        string.IsNullOrWhiteSpace(externalTaskId) ? null : externalTaskId.Trim();
}
