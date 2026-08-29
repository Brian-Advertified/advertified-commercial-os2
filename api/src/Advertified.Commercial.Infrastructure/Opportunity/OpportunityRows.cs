using System.Text.Json;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Opportunity;

internal sealed record OpportunityRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceRef { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public long? ExpectedValueMinor { get; set; }
    public string? Currency { get; set; }
    public DateOnly? Deadline { get; set; }
    public string? ProblemSummary { get; set; }
    public string? ObjectiveSummary { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record EvidenceSourceRow
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Locator { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string PolicyBasis { get; set; } = string.Empty;
    public string CaptureStatus { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
}

internal sealed record EvidenceItemRow
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string Locator { get; set; } = string.Empty;
    public string ClaimType { get; set; } = string.Empty;
    public string OriginalValueJson { get; set; } = "{}";
    public string? ReviewedValueJson { get; set; }
    public string Excerpt { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? Decision { get; set; }
    public string? ReviewReason { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ReviewedBy { get; set; }
    public long Version { get; set; }
}

internal sealed record EvidenceSetRow
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public int VersionNumber { get; set; }
    public Guid[] EvidenceItemIds { get; set; } = [];
    public string GapsJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public long Version { get; set; }
}

internal sealed record InterpretationRow
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid EvidenceSetId { get; set; }
    public int VersionNumber { get; set; }
    public string ArtifactJson { get; set; } = "{}";
    public string EvidenceBindingsJson { get; set; } = "[]";
    public string UnknownsJson { get; set; } = "[]";
    public string AssumptionsJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public long Version { get; set; }
}

internal sealed record AngleRow
{
    public Guid Id { get; set; }
    public Guid AngleSetId { get; set; }
    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string EvidenceItemIdsJson { get; set; } = "[]";
    public decimal Confidence { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? SelectedBy { get; set; }
    public long Version { get; set; }
}

internal sealed record StrategyRow
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public int VersionNumber { get; set; }
    public string ArtifactJson { get; set; } = "{}";
    public string EvidenceBindingsJson { get; set; } = "[]";
    public string UnknownsJson { get; set; } = "[]";
    public string AssumptionsJson { get; set; } = "[]";
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? SubmittedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public long Version { get; set; }
}

internal sealed record ObjectionRow
{
    public Guid Id { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string FieldPath { get; set; } = string.Empty;
    public string EvidenceGap { get; set; } = string.Empty;
    public string RecommendedResolution { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public string? ResolutionReason { get; set; }
    public Guid? ResolvedBy { get; set; }
    public long Version { get; set; }
}

internal sealed record AgentRunRow
{
    public Guid Id { get; set; }
    public Guid OpportunityId { get; set; }
    public string RunKind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public int Attempts { get; set; }
    public string? ErrorCode { get; set; }
    public long IncrementalCostMinor { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record HumanTaskRow
{
    public Guid Id { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? BriefId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string WhyItMatters { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public long ResourceVersion { get; set; }
    public Guid AssigneeUserId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed record RunWorkRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OpportunityId { get; set; }
    public string RunKind { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long InputVersion { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? ApproverUserId { get; set; }
    public Guid CorrelationId { get; set; }
    public int Attempts { get; set; }
    public long Version { get; set; }
}

internal sealed record ApprovedEvidenceRow
{
    public Guid EvidenceSetId { get; set; }
    public int EvidenceSetVersion { get; set; }
    public Guid Id { get; set; }
    public string ClaimType { get; set; } = string.Empty;
    public string StructuredValueJson { get; set; } = "{}";
    public string Excerpt { get; set; } = string.Empty;
}

internal sealed record ObjectionContextRow
{
    public Guid StrategyId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid StrategyCreatorId { get; set; }
    public Guid? ApproverUserId { get; set; }
}

internal static class OpportunityRowMapper
{
    public static OpportunityView ToView(this OpportunityRow row) => new(
        row.Id, row.TenantId, row.ClientId, row.Title, row.SourceType, row.SourceRef,
        row.OwnerUserId, row.Stage, row.ExpectedValueMinor, row.Currency, row.Deadline,
        row.ProblemSummary, row.ObjectiveSummary, row.Version, row.UpdatedAtUtc);

    public static EvidenceSourceView ToView(this EvidenceSourceRow row) => new(
        row.Id, row.OpportunityId, row.Type, row.Locator, row.Title, row.ContentHash,
        row.PolicyBasis, row.CaptureStatus, row.Version, row.CapturedAtUtc);

    public static EvidenceItemView ToView(this EvidenceItemRow row) => new(
        row.Id, row.SourceId, row.Locator, row.ClaimType, row.OriginalValueJson,
        row.ReviewedValueJson, row.Excerpt, row.Confidence, row.ReviewStatus, row.Decision,
        row.ReviewReason, row.CreatedBy, row.ReviewedBy, row.Version);

    public static EvidenceSetView ToView(this EvidenceSetRow row) => new(
        row.Id, row.OpportunityId, row.VersionNumber, row.EvidenceItemIds,
        JsonSerializer.Deserialize<string[]>(row.GapsJson) ?? [], row.Status,
        row.CreatedBy, row.ApprovedBy, row.Version);

    public static BusinessInterpretationView ToView(this InterpretationRow row) => new(
        row.Id, row.OpportunityId, row.EvidenceSetId, row.VersionNumber, row.ArtifactJson,
        row.EvidenceBindingsJson, row.UnknownsJson, row.AssumptionsJson, row.Status,
        row.CreatedBy, row.ConfirmedBy, row.Version);

    public static OpportunityAngleView ToView(this AngleRow row) => new(
        row.Id, row.AngleSetId, row.Rank, row.Title, row.Rationale,
        row.EvidenceItemIdsJson, row.Confidence, row.Status, row.SelectedBy, row.Version);

    public static CriticObjectionView ToView(this ObjectionRow row) => new(
        row.Id, row.Severity, row.FieldPath, row.EvidenceGap, row.RecommendedResolution,
        row.Resolution, row.ResolutionReason, row.ResolvedBy, row.Version);

    public static AgentRunView ToView(this AgentRunRow row) => new(
        row.Id, row.OpportunityId, row.RunKind, row.Status, row.CurrentStep, row.Attempts,
        row.ErrorCode, Recovery(row), row.IncrementalCostMinor, row.Version, row.UpdatedAtUtc);

    public static HumanTaskView ToView(this HumanTaskRow row) => new(
        row.Id, row.OpportunityId, row.BriefId, row.TaskType, row.Status, row.Title, row.WhyItMatters,
        row.ResourceType, row.ResourceId, row.ResourceVersion, row.AssigneeUserId,
        row.Version, row.CreatedAtUtc);

    private static string? Recovery(AgentRunRow row) => row.Status switch
    {
        MasterDataCodes.LifecycleStatuses.ReviewRequired =>
            "Review the recorded issue, then resume from the safe checkpoint.",
        MasterDataCodes.LifecycleStatuses.Failed => "Inspect the business-safe error and create a corrected run.",
        _ => null,
    };
}
