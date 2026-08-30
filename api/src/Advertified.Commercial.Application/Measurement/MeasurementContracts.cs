using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Measurement;

public sealed record PerformanceEvidenceFileUpload(
    string FileName,
    string MediaType,
    byte[] Content);

public sealed record PerformanceMetricInput(
    string MetricType,
    decimal Value,
    string Unit,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceLocator);

public sealed record SubmitPerformanceEvidenceCommand(
    string SourceReference,
    DateTimeOffset CapturedAtUtc,
    string Methodology,
    IReadOnlyList<string> Limitations,
    string QualityStatus,
    Guid ReviewerUserId,
    IReadOnlyList<PerformanceMetricInput> Metrics,
    PerformanceEvidenceFileUpload File);

public sealed record ReviewPerformanceEvidenceCommand(bool Approved, string Reason);

public sealed record PerformanceMetricView(
    Guid Id,
    string MetricType,
    decimal Value,
    string Unit,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceLocator);

public sealed record PerformanceEvidenceView(
    Guid Id,
    Guid CampaignId,
    string SourceReference,
    string FileName,
    string MediaType,
    long SizeBytes,
    string ContentSha256,
    bool SignatureValidated,
    string MalwareScanStatus,
    DateTimeOffset CapturedAtUtc,
    string Methodology,
    IReadOnlyList<string> Limitations,
    string QualityStatus,
    IReadOnlyList<PerformanceMetricView> Metrics,
    string Status,
    Guid ReviewerUserId,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public interface IPerformanceEvidenceCommands
{
    Task<CommandResult<PerformanceEvidenceView>> SubmitAsync(
        Guid campaignId,
        CommandEnvelope<SubmitPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PerformanceEvidenceView>> ReviewAsync(
        Guid campaignId,
        Guid evidenceId,
        CommandEnvelope<ReviewPerformanceEvidenceCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IPerformanceEvidenceReader
{
    Task<PerformanceEvidenceView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid evidenceId,
        CancellationToken cancellationToken);
}

public sealed class PerformanceEvidenceBlockedException : Exception;
public sealed class PerformanceEvidenceFileRejectedException : Exception;
