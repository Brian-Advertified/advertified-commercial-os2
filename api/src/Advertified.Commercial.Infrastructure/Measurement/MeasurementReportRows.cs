using System.Text.Json;
using Advertified.Commercial.Application.Measurement;

namespace Advertified.Commercial.Infrastructure.Measurement;

internal sealed record MeasurementReportSourceRow(
    Guid TenantId,
    Guid CampaignId,
    Guid? OpportunityId,
    long CampaignVersion,
    string MeasurementPlanJson,
    Guid ApproverUserId);

internal sealed record MeasurementEvidenceVersion(Guid Id, long Version);

internal sealed record MeasurementReportRow(
    Guid Id,
    Guid TenantId,
    Guid CampaignId,
    int VersionNumber,
    Guid AgentRunId,
    long CampaignVersion,
    string MeasurementPlanJson,
    string EvidenceVersionsJson,
    string InterpretationJson,
    string Status,
    Guid ApproverUserId,
    Guid GeneratedBy,
    DateTimeOffset GeneratedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version,
    DateTimeOffset UpdatedAtUtc)
{
    internal string[] MeasurementPlan() =>
        JsonSerializer.Deserialize<string[]>(MeasurementPlanJson) ?? [];

    internal MeasurementEvidenceVersion[] EvidenceVersions() =>
        JsonSerializer.Deserialize<MeasurementEvidenceVersion[]>(
            EvidenceVersionsJson, MeasurementReportRecordStore.StoredJson) ?? [];

    internal MeasurementInterpretationView Interpretation() =>
        JsonSerializer.Deserialize<MeasurementInterpretationView>(
            InterpretationJson, MeasurementReportRecordStore.StoredJson)
        ?? throw new InvalidOperationException("The measurement report is unavailable.");
}
