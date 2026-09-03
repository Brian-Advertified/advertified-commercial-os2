using System.Runtime.CompilerServices;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryExtractionAttemptStore
{
    internal Task<bool> MarkSubmittingAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken) => TransitionAsync(
            claim,
            MasterDataCodes.InventoryExtractionAttemptStatuses.Pending,
            MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
            $"""
            submitted_at_utc = {UtcNow}, provider_response_code = {"SUBMITTING"},
            provider_error_code = NULL, failure_class_collection_code = NULL,
            failure_class_code = NULL
            """,
            cancellationToken);

    internal Task<bool> MarkRunningAsync(
        InventoryExtractionWorkerClaim claim,
        string expectedStatus,
        string externalTaskId,
        InventoryExtractionSubmission submission,
        CancellationToken cancellationToken) => TransitionAsync(
            claim,
            expectedStatus,
            MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
            $"""
            external_task_id = {externalTaskId},
            started_at_utc = CASE WHEN {submission.State == InventoryProviderTaskState.Running}
                THEN COALESCE(started_at_utc, {UtcNow}) ELSE started_at_utc END,
            provider_response_code = {submission.ProviderResponseCode},
            provider_error_code = NULL, polling_checkpoint = {submission.PollingCheckpointJson}::jsonb,
            failure_class_collection_code = NULL, failure_class_code = NULL
            """,
            cancellationToken);

    internal Task<bool> ResumeRetryableAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken) => TransitionAsync(
            claim,
            MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable,
            MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
            $"""
            provider_error_code = NULL, failure_class_collection_code = NULL,
            failure_class_code = NULL
            """,
            cancellationToken);

    internal async Task<bool> RecordPollAsync(
        InventoryExtractionWorkerClaim claim,
        InventoryExtractionPollResult poll,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginSessionAsync(claim, cancellationToken);
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.inventory_extraction_attempts
            SET started_at_utc = CASE WHEN {poll.State == InventoryProviderTaskState.Running}
                    THEN COALESCE(started_at_utc, {UtcNow}) ELSE started_at_utc END,
                last_polled_at_utc = {UtcNow},
                polling_checkpoint = {poll.PollingCheckpointJson}::jsonb,
                provider_response_code = {poll.ProviderResponseCode},
                provider_error_code = {poll.ProviderErrorCode},
                worker_id = NULL, worker_lease_token = NULL,
                worker_lease_expires_at_utc = NULL
            WHERE tenant_id = {claim.TenantId} AND id = {claim.AttemptId}
              AND status_code = {MasterDataCodes.InventoryExtractionAttemptStatuses.Running}
              AND worker_lease_token = {claim.ClaimToken}
              AND worker_lease_expires_at_utc > {UtcNow}
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed == 1;
    }

    internal Task<bool> MarkFailureAsync(
        InventoryExtractionWorkerClaim claim,
        string expectedStatus,
        string nextStatus,
        string failureClass,
        string responseCode,
        string? errorCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        var terminal = nextStatus !=
            MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable;
        return TransitionAsync(
            claim,
            expectedStatus,
            nextStatus,
            $"""
            completed_at_utc = CASE WHEN {terminal} THEN {UtcNow} ELSE NULL END,
            provider_response_code = {responseCode}, provider_error_code = {errorCode},
            failure_class_collection_code =
                {MasterDataCodes.InventoryExtractionFailureClasses.Collection},
            failure_class_code = {failureClass}, reconciliation_notes = {notes},
            worker_id = NULL, worker_lease_token = NULL,
            worker_lease_expires_at_utc = NULL
            """,
            cancellationToken);
    }

    private async Task<bool> TransitionAsync(
        InventoryExtractionWorkerClaim claim,
        string expectedStatus,
        string nextStatus,
        FormattableString changes,
        CancellationToken cancellationToken)
    {
        InventoryExtractionAttemptStateMachine.EnsureTransition(expectedStatus, nextStatus);
        await using var transaction = await BeginSessionAsync(claim, cancellationToken);
        var arguments = changes.GetArguments().Concat([
            nextStatus, claim.TenantId, claim.AttemptId, expectedStatus,
            claim.ClaimToken, UtcNow,
        ]).ToArray();
        var start = changes.Format + ", status_code = {" + (arguments.Length - 6) + "}";
        var where = " WHERE tenant_id = {" + (arguments.Length - 5) + "}" +
            " AND id = {" + (arguments.Length - 4) + "}" +
            " AND status_code = {" + (arguments.Length - 3) + "}" +
            " AND worker_lease_token = {" + (arguments.Length - 2) + "}" +
            " AND worker_lease_expires_at_utc > {" + (arguments.Length - 1) + "}";
        var sql = FormattableStringFactory.Create(
            "UPDATE commercial.inventory_extraction_attempts SET " + start + where,
            arguments);
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync(
            sql, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return changed == 1;
    }
}
