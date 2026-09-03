namespace Advertified.Commercial.Infrastructure.Worker;

public sealed class WorkerDispatchOptions
{
    public const string SectionName = "WorkerDispatch";

    public int PollMilliseconds { get; init; } = 500;
    public int EmailLeaseSeconds { get; init; } = 120;
    public int FailureDelaySeconds { get; init; } = 60;
    public int MaxEmailAttempts { get; init; } = 5;
    public int InventoryExtractionLeaseSeconds { get; init; } = 120;
    public int InventoryExtractionMaxConcurrency { get; init; } = 1;

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollMilliseconds);

    public static bool HasSafeTiming(WorkerDispatchOptions options) =>
        options.PollMilliseconds is >= 100 and <= 5_000 &&
        options.EmailLeaseSeconds is >= 30 and <= 600 &&
        options.FailureDelaySeconds is >= 15 and <= 900 &&
        options.MaxEmailAttempts is >= 1 and <= 20 &&
        options.InventoryExtractionLeaseSeconds is >= 30 and <= 600 &&
        options.InventoryExtractionMaxConcurrency is >= 1 and <= 4;
}

public static class EmailWorkerCompletion
{
    public const string Completed = "completed";
    public const string RetryScheduled = "retry_scheduled";
    public const string DeadLettered = "dead_lettered";
    public const string Fenced = "fenced";
}

public sealed record EmailWorkerClaim(
    Guid TenantId,
    Guid InboundEmailId,
    Guid OwnerUserId,
    Guid CorrelationId,
    Guid ClaimToken);

public sealed record InventoryExtractionWorkerClaim(
    Guid TenantId,
    Guid AttemptId,
    Guid ImportId,
    long SourceFileVersion,
    string SourceHash,
    string Status,
    string StableSubmissionKey,
    string ProviderName,
    string ProviderVersion,
    string? ExternalTaskId,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? LastPolledAtUtc,
    string PollingCheckpointJson,
    int AttemptNumber,
    Guid RequestedBy,
    Guid CommandId,
    Guid CorrelationId,
    Guid ClaimToken);
