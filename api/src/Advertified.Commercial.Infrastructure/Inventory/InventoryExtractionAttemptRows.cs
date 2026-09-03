using Advertified.Commercial.Application.Inventory;

namespace Advertified.Commercial.Infrastructure.Inventory;

internal sealed record InventoryExtractionAttemptRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportId { get; set; }
    public long SourceFileVersion { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string StableSubmissionKey { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExternalTaskId { get; set; }
    public DateTimeOffset? SubmittedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? LastPolledAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string PollingCheckpointJson { get; set; } = "{}";
    public int AttemptNumber { get; set; }
    public Guid? WorkerId { get; set; }
    public DateTimeOffset? WorkerLeaseExpiresAtUtc { get; set; }
    public string? ProviderResponseCode { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? FailureClassification { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? ExtractedArtifactId { get; set; }
    public string? ReconciliationNotes { get; set; }
    public long Version { get; set; }

    internal InventoryExtractionAttemptView ToView() => new(
        Id, TenantId, ImportId, SourceFileVersion, SourceHash, StableSubmissionKey,
        ProviderName, ProviderVersion, Status, ExternalTaskId, SubmittedAtUtc,
        StartedAtUtc, LastPolledAtUtc, CompletedAtUtc, PollingCheckpointJson,
        AttemptNumber, WorkerId, WorkerLeaseExpiresAtUtc, ProviderResponseCode,
        ProviderErrorCode, FailureClassification, CorrelationId,
        ExtractedArtifactId, ReconciliationNotes, Version);
}
