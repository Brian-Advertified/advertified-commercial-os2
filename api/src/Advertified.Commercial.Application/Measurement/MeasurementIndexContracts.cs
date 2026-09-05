namespace Advertified.Commercial.Application.Measurement;

public sealed record MeasurementCampaignSummary(
    Guid Id, string Title, string Status, int EvidenceCount,
    int ReportCount, DateTimeOffset UpdatedAtUtc);

public sealed record MeasurementReportSummary(
    Guid Id, Guid CampaignId, string CampaignTitle, int VersionNumber,
    string Status, int EvidenceCount, DateTimeOffset UpdatedAtUtc);

public sealed record MeasurementCampaignPage(
    IReadOnlyList<MeasurementCampaignSummary> Items, Guid? NextCursor);

public sealed record MeasurementReportPage(
    IReadOnlyList<MeasurementReportSummary> Items, Guid? NextCursor);
