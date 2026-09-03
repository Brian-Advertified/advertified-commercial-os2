namespace Advertified.Commercial.Application.Inventory;

public enum InventoryProviderTaskState
{
    Pending,
    Running,
    Completed,
    Failed,
}

public sealed record InventoryExtractionSubmission(
    string ExternalTaskId,
    InventoryProviderTaskState State,
    string ProviderResponseCode,
    string PollingCheckpointJson);

public sealed record InventoryExtractionPollResult(
    InventoryProviderTaskState State,
    string ProviderResponseCode,
    string? ProviderErrorCode,
    string PollingCheckpointJson);

public sealed record InventoryExtractionAttemptView(
    Guid Id,
    Guid TenantId,
    Guid InventoryImportId,
    long SourceFileVersion,
    string SourceHash,
    string StableSubmissionKey,
    string ProviderName,
    string ProviderVersion,
    string Status,
    string? ExternalTaskId,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastPolledAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string PollingCheckpointJson,
    int AttemptNumber,
    Guid? WorkerId,
    DateTimeOffset? WorkerLeaseExpiresAtUtc,
    string? ProviderResponseCode,
    string? ProviderErrorCode,
    string? FailureClassification,
    Guid CorrelationId,
    Guid? ExtractedArtifactId,
    string? ReconciliationNotes,
    long Version);

public interface IDurableInventoryDocumentExtractionAdapter :
    IInventoryDocumentExtractionAdapter
{
    string ProviderName { get; }
    string ProviderVersion { get; }
    bool SupportsIdempotentSubmission { get; }
    bool SupportsCancellation { get; }

    Task<InventoryExtractionSubmission> SubmitAsync(
        InventoryExtractionRequest request,
        string stableSubmissionKey,
        CancellationToken cancellationToken);

    Task<InventoryExtractionPollResult> PollAsync(
        string externalTaskId,
        CancellationToken cancellationToken);

    Task<InventoryExtractionResult> ReadResultAsync(
        InventoryExtractionRequest request,
        string externalTaskId,
        CancellationToken cancellationToken);

    Task<bool> CancelAsync(
        string externalTaskId,
        CancellationToken cancellationToken);
}

public sealed class InventoryExtractionSubmissionRejectedException(
    string responseCode) : Exception("Document extraction submission was rejected.")
{
    public string ResponseCode { get; } = responseCode;
}
