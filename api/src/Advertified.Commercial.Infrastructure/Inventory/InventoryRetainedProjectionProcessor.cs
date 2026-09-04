using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryRetainedProjectionProcessor(
    InventoryExtractionAttemptStore store,
    DoclingInventoryExtractionAdapter doclingExtraction,
    InventorySemanticEnrichmentService semanticEnrichment,
    InventoryReprojectionCompletionService completion,
    ILogger<InventoryRetainedProjectionProcessor> logger)
{
    internal async Task ProcessAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        EnsureRetainedProvider(claim);
        var runnable = await PrepareClaimAsync(
            claim, cancellationToken);
        if (runnable is null) return;

        try
        {
            await ReprojectAsync(runnable, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = MapFailure(exception);
            if (failure is null)
            {
                LogReprojectionRejected(
                    logger, exception, runnable.AttemptId,
                    runnable.CorrelationId);
                failure = UnexpectedFailure(exception);
            }
            await FailAsync(
                runnable, failure.Status, failure.Code,
                failure.Notes, cancellationToken);
        }
    }

    private static void EnsureRetainedProvider(
        InventoryExtractionWorkerClaim claim)
    {
        if (claim.ProviderName !=
            InventoryReprojectionPolicy.ProviderName)
        {
            throw new InvalidOperationException(
                "The attempt is not a retained projection.");
        }
    }

    private async Task<InventoryExtractionWorkerClaim?>
        PrepareClaimAsync(
            InventoryExtractionWorkerClaim claim,
            CancellationToken cancellationToken)
    {
        if (claim.Status ==
            MasterDataCodes.InventoryExtractionAttemptStatuses.Pending)
        {
            if (!await store.MarkSubmittingAsync(
                    claim, cancellationToken))
            {
                return null;
            }
            claim = claim with
            {
                Status = MasterDataCodes
                    .InventoryExtractionAttemptStatuses.Submitting,
            };
        }
        if (claim.Status ==
            MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting)
        {
            return await StartAsync(claim, cancellationToken);
        }
        if (claim.Status ==
            MasterDataCodes.InventoryExtractionAttemptStatuses
                .FailedRetryable)
        {
            return await ResumeAsync(claim, cancellationToken);
        }
        return claim.Status ==
            MasterDataCodes.InventoryExtractionAttemptStatuses.Running
            ? claim
            : null;
    }

    private async Task<InventoryExtractionWorkerClaim?> StartAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        var externalTaskId =
            InventoryReprojectionPolicy.ExternalTaskId(
                claim.AttemptId);
        var submission = new InventoryExtractionSubmission(
            externalTaskId,
            InventoryProviderTaskState.Running,
            "LOCAL_REPROJECTION_STARTED",
            "{}");
        if (!await store.MarkRunningAsync(
                claim,
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .Submitting,
                externalTaskId, submission, cancellationToken))
        {
            return null;
        }
        return claim with
        {
            Status = MasterDataCodes
                .InventoryExtractionAttemptStatuses.Running,
            ExternalTaskId = externalTaskId,
        };
    }

    private async Task<InventoryExtractionWorkerClaim?> ResumeAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        if (!await store.ResumeRetryableAsync(
                claim, cancellationToken))
        {
            return null;
        }
        return claim with
        {
            Status = MasterDataCodes
                .InventoryExtractionAttemptStatuses.Running,
        };
    }

    private async Task ReprojectAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                claim.ProviderVersion,
                semanticEnrichment.CurrentProjectionVersion,
                StringComparison.Ordinal))
        {
            throw new
                InventoryProjectionConfigurationChangedException();
        }
        var source = await store.ReadProjectionSourceAsync(
            claim, cancellationToken);
        var extraction = await doclingExtraction
            .ReprojectRetainedAsync(
                source.Request,
                source.ProviderJson,
                cancellationToken);
        extraction = await semanticEnrichment.EnrichAsync(
            claim, extraction, cancellationToken);
        await completion.ApplyAsync(
            claim, source.InputArtifactId,
            extraction, cancellationToken);
    }

    private static ReprojectionFailure? MapFailure(
        Exception exception) => exception switch
    {
        InventoryProjectionConfigurationChangedException =>
            new(
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .FailedTerminal,
                "PROJECTION_CONFIGURATION_CHANGED",
                "The projection configuration changed after queueing. Queue a fresh versioned reprojection."),
        InventorySemanticReconciliationRequiredException =>
            new(
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .ReconciliationRequired,
                "SEMANTIC_RECONCILIATION_REQUIRED",
                "The Bedrock result is ambiguous and must be reconciled before another call."),
        InventorySemanticResultRejectedException rejected =>
            new(
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .FailedTerminal,
                "SEMANTIC_RESULT_REJECTED_" + rejected.Message,
                "Bedrock returned a result, but governed validation rejected it. The rejected output and provider usage are retained; no artifact was accepted."),
        InventorySemanticInputRejectedException =>
            new(
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .FailedTerminal,
                "SEMANTIC_INPUT_NOT_SUPPORTED",
                "The bounded semantic input cannot safely represent every required source image."),
        InventorySemanticBudgetExceededException =>
            new(
                MasterDataCodes.InventoryExtractionAttemptStatuses
                    .FailedTerminal,
                "SEMANTIC_BUDGET_EXCEEDED",
                "The bounded semantic plan exceeds the approved budget."),
        _ => null,
    };

    private static ReprojectionFailure UnexpectedFailure(
        Exception exception)
    {
        var status = MasterDataCodes
            .InventoryExtractionAttemptStatuses.FailedTerminal;
        return exception switch
        {
            InventoryProtectionUnavailableException => new(
                status,
                "REPROJECTION_SOURCE_HASH_MISMATCH",
                "The protected source no longer matches its recorded hash."),
            InventoryExtractionUnavailableException => new(
                status,
                "LOCAL_OCR_EXTRACTION_UNAVAILABLE",
                "Local Docling could not safely reconstruct the embedded source image."),
            ApprovalRequiredException => new(
                status,
                "REPROJECTION_REVIEWER_REQUIRED",
                "A separate eligible inventory reviewer is required."),
            InvalidLifecycleTransitionException => new(
                status,
                "REPROJECTION_LIFECYCLE_CONFLICT",
                "The import changed while the local reprojection was completing."),
            PostgresException postgres => new(
                status,
                "REPROJECTION_DATABASE_" + postgres.SqlState,
                "The database rejected the local reprojection at constraint " +
                (postgres.ConstraintName ?? "unknown") + "."),
            DbUpdateException => new(
                status,
                "REPROJECTION_PERSISTENCE_FAILED",
                "The local reprojection could not be persisted."),
            HttpRequestException => new(
                status,
                "LOCAL_OCR_TRANSPORT_FAILED",
                "The local Docling service could not be reached."),
            TaskCanceledException => new(
                status,
                "LOCAL_OCR_TIMEOUT",
                "The bounded local Docling operation timed out."),
            InvalidOperationException invalid => new(
                status,
                "REPROJECTION_INVARIANT_FAILED",
                "Local reprojection invariant: " +
                SafeDiagnostic(invalid.Message)),
            _ => new(
                status,
                "REPROJECTION_UNEXPECTED_" +
                exception.GetType().Name.ToUpperInvariant(),
                "Local reprojection failed in " +
                exception.GetType().Name + "."),
        };
    }

    private static string SafeDiagnostic(string value)
    {
        var normalized = string.Join(' ', value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries));
        return normalized.Length <= 240
            ? normalized
            : normalized[..240];
    }

    private sealed record ReprojectionFailure(
        string Status,
        string Code,
        string Notes);

    private Task FailAsync(
        InventoryExtractionWorkerClaim claim,
        string status,
        string failureCode,
        string notes,
        CancellationToken cancellationToken) =>
        store.FailReprojectionAsync(
            claim, status, failureCode, notes,
            cancellationToken);

    [LoggerMessage(
        EventId = 12_411,
        Level = LogLevel.Error,
        Message = "Inventory retained reprojection was rejected. AttemptId={AttemptId} CorrelationId={CorrelationId}")]
    private static partial void LogReprojectionRejected(
        ILogger logger,
        Exception exception,
        Guid attemptId,
        Guid correlationId);
}

internal sealed class
    InventoryProjectionConfigurationChangedException : Exception
{
}
