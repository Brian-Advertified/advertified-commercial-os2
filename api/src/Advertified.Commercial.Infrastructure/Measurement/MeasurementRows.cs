using System.Text.Json;
using Advertified.Commercial.Application.Measurement;

namespace Advertified.Commercial.Infrastructure.Measurement;

internal sealed record PerformanceEvidenceSourceRow(
    Guid TenantId,
    Guid CampaignId,
    DateOnly CampaignStart,
    DateOnly CampaignEnd,
    DateTimeOffset CompletedAtUtc,
    Guid ReviewerUserId);

internal sealed record PerformanceEvidenceRow(
    Guid Id,
    Guid TenantId,
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
    string LimitationsJson,
    string QualityStatus,
    string Status,
    Guid ReviewerUserId,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version,
    DateTimeOffset UpdatedAtUtc);

internal sealed record PerformanceMetricRow(
    Guid Id,
    Guid EvidenceId,
    string MetricType,
    decimal Value,
    string Unit,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceLocator)
{
    internal PerformanceMetricView ToView() => new(
        Id, MetricType, Value, Unit, PeriodStart, PeriodEnd, SourceLocator);
}

internal static class PerformanceEvidenceRowMapper
{
    internal static PerformanceEvidenceView ToView(
        PerformanceEvidenceRow row,
        IReadOnlyList<PerformanceMetricRow> metrics) => new(
            row.Id, row.CampaignId, row.SourceReference, row.FileName, row.MediaType,
            row.SizeBytes, row.ContentSha256, row.SignatureValidated,
            row.MalwareScanStatus, row.CapturedAtUtc, row.Methodology,
            JsonSerializer.Deserialize<string[]>(row.LimitationsJson) ?? [],
            row.QualityStatus, metrics.Select(item => item.ToView()).ToArray(),
            row.Status, row.ReviewerUserId, row.SubmittedBy, row.SubmittedAtUtc,
            row.ReviewedBy, row.ReviewedAtUtc, row.ReviewReason, row.Version,
            row.UpdatedAtUtc);
}

internal sealed record PreparedPerformanceEvidence(
    string SourceReference,
    DateTimeOffset CapturedAtUtc,
    string Methodology,
    string[] Limitations,
    string QualityStatus,
    Guid ReviewerUserId,
    string FileName,
    string MediaType,
    byte[] Content,
    string Sha256,
    PreparedPerformanceMetric[] Metrics);

internal sealed record PreparedPerformanceMetric(
    string MetricType,
    decimal Value,
    string Unit,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceLocator);
