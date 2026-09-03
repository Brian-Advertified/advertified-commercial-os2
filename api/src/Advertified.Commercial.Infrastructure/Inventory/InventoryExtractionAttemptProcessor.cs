using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Worker;
using Microsoft.Extensions.Logging;

namespace Advertified.Commercial.Infrastructure.Inventory;

public sealed partial class InventoryExtractionAttemptProcessor(
    InventoryExtractionAttemptStore store,
    InventoryExtractionCompletionService completion,
    IInventoryDocumentExtractionAdapter extractionAdapter,
    TimeProvider timeProvider,
    ILogger<InventoryExtractionAttemptProcessor> logger)
{
    public async Task ProcessAsync(
        InventoryExtractionWorkerClaim claim,
        CancellationToken cancellationToken)
    {
        if (extractionAdapter is not IDurableInventoryDocumentExtractionAdapter provider ||
            !MatchesProvider(claim, provider))
        {
            await ReconcileAsync(
                claim, claim.Status, "Configured extraction provider does not match the attempt.",
                cancellationToken);
            return;
        }
        switch (claim.Status)
        {
            case MasterDataCodes.InventoryExtractionAttemptStatuses.Pending:
                await SubmitAsync(claim, provider, cancellationToken);
                return;
            case MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting:
                await ReconcileAsync(
                    claim, claim.Status,
                    "Submission was interrupted before its external task ID was durable.",
                    cancellationToken);
                return;
            case MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable:
                if (!await store.ResumeRetryableAsync(claim, cancellationToken)) return;
                await PollAsync(claim, provider, cancellationToken);
                return;
            case MasterDataCodes.InventoryExtractionAttemptStatuses.Running:
                await PollAsync(claim, provider, cancellationToken);
                return;
            default:
                return;
        }
    }

    private async Task SubmitAsync(
        InventoryExtractionWorkerClaim claim,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        InventoryExtractionSource source;
        try
        {
            source = await store.ReadSourceAsync(claim, cancellationToken);
            InventoryExtractionCompletionPolicy.VerifySource(
                source.Request.Content, claim.SourceHash);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Pending,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionFailureClasses.SourceUnavailable,
                MasterDataCodes.InventoryExtractionFailureClasses.SourceUnavailable,
                "PROTECTED_SOURCE_UNAVAILABLE", null,
                cancellationToken);
            return;
        }
        if (!await store.MarkSubmittingAsync(claim, cancellationToken)) return;
        InventoryExtractionSubmission submission;
        try
        {
            submission = await provider.SubmitAsync(
                source.Request, claim.StableSubmissionKey, cancellationToken);
        }
        catch (InventoryExtractionSubmissionRejectedException rejected)
        {
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionFailureClasses.ProviderTerminal,
                rejected.ResponseCode, "SUBMISSION_REJECTED", null, cancellationToken);
            return;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await ReconcileAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
                "Docling submission response was lost or could not be interpreted.",
                cancellationToken);
            return;
        }
        var persisted = await store.MarkRunningAsync(
            claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Submitting,
            submission.ExternalTaskId, submission, cancellationToken);
        if (!persisted)
        {
            throw new InvalidOperationException(
                "The accepted provider task identifier could not be persisted.");
        }
        await PollAsync(
            claim with
            {
                Status = MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                ExternalTaskId = submission.ExternalTaskId,
                SubmittedAtUtc = timeProvider.GetUtcNow(),
            }, provider, cancellationToken);
    }

    private async Task PollAsync(
        InventoryExtractionWorkerClaim claim,
        IDurableInventoryDocumentExtractionAdapter provider,
        CancellationToken cancellationToken)
    {
        if (claim.ExternalTaskId is null || claim.SubmittedAtUtc is null)
        {
            await ReconcileAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                "The running attempt has no durable task identifier or submission time.",
                cancellationToken);
            return;
        }
        if (InventoryExtractionAttemptStateMachine.HasTimedOut(
                claim.SubmittedAtUtc.Value, timeProvider.GetUtcNow()))
        {
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.TimedOut,
                MasterDataCodes.InventoryExtractionFailureClasses.Timeout,
                "TASK_TIMEOUT", "MAXIMUM_DURATION_EXCEEDED",
                "Ordinary polling stopped after 3,600 seconds.", cancellationToken);
            return;
        }
        InventoryExtractionPollResult poll;
        try
        {
            poll = await provider.PollAsync(claim.ExternalTaskId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable,
                MasterDataCodes.InventoryExtractionFailureClasses.ProviderRetryable,
                "POLL_UNAVAILABLE", "PROVIDER_POLL_FAILED", null, cancellationToken);
            return;
        }
        if (poll.State == InventoryProviderTaskState.Failed)
        {
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedTerminal,
                MasterDataCodes.InventoryExtractionFailureClasses.ProviderTerminal,
                poll.ProviderResponseCode, poll.ProviderErrorCode, null, cancellationToken);
            return;
        }
        if (poll.State != InventoryProviderTaskState.Completed)
        {
            await store.RecordPollAsync(claim, poll, cancellationToken);
            return;
        }
        await CompleteAsync(claim, provider, poll, cancellationToken);
    }

    private async Task CompleteAsync(
        InventoryExtractionWorkerClaim claim,
        IDurableInventoryDocumentExtractionAdapter provider,
        InventoryExtractionPollResult poll,
        CancellationToken cancellationToken)
    {
        try
        {
            var source = await store.ReadSourceAsync(claim, cancellationToken);
            var result = await provider.ReadResultAsync(
                source.Request, claim.ExternalTaskId!, cancellationToken);
            await completion.ApplyAsync(claim, result, poll, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogResultRejected(
                logger, exception, claim.AttemptId, claim.CorrelationId,
                claim.ExternalTaskId!);
            await store.MarkFailureAsync(
                claim, MasterDataCodes.InventoryExtractionAttemptStatuses.Running,
                MasterDataCodes.InventoryExtractionAttemptStatuses.FailedRetryable,
                MasterDataCodes.InventoryExtractionFailureClasses.InvalidResult,
                poll.ProviderResponseCode, "RESULT_NOT_ACCEPTED", null, cancellationToken);
        }
    }

    [LoggerMessage(
        EventId = 12_403,
        Level = LogLevel.Warning,
        Message = "Provider result was fenced before acceptance. AttemptId={AttemptId} CorrelationId={CorrelationId} ExternalTaskId={ExternalTaskId}")]
    private static partial void LogResultRejected(
        ILogger logger,
        Exception exception,
        Guid attemptId,
        Guid correlationId,
        string externalTaskId);

    private Task<bool> ReconcileAsync(
        InventoryExtractionWorkerClaim claim,
        string expectedStatus,
        string notes,
        CancellationToken cancellationToken) => store.MarkFailureAsync(
            claim, expectedStatus,
            MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
            MasterDataCodes.InventoryExtractionFailureClasses.AmbiguousSubmission,
            MasterDataCodes.InventoryExtractionAttemptStatuses.ReconciliationRequired,
            "AMBIGUOUS_PROVIDER_ACCEPTANCE", notes,
            cancellationToken);

    private static bool MatchesProvider(
        InventoryExtractionWorkerClaim claim,
        IDurableInventoryDocumentExtractionAdapter provider) =>
        string.Equals(claim.ProviderName, provider.ProviderName, StringComparison.Ordinal) &&
        string.Equals(claim.ProviderVersion, provider.ProviderVersion, StringComparison.Ordinal);
}
