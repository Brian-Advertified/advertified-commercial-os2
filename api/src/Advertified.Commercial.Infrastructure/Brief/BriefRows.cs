using System.Text.Json;
using Advertified.Commercial.Application.Brief;

namespace Advertified.Commercial.Infrastructure.Brief;

internal sealed record CampaignBriefRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public Guid? OpportunityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid OwnerUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CurrentDraftVersionId { get; set; }
    public Guid? ReadyVersionId { get; set; }
    public Guid? ApprovedVersionId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed record BriefSourceRow
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string Locator { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal sealed record BriefVersionRow
{
    public Guid Id { get; set; }
    public Guid BriefId { get; set; }
    public Guid? BaseVersionId { get; set; }
    public Guid SourceId { get; set; }
    public int VersionNumber { get; set; }
    public string BusinessProblem { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string AudiencesJson { get; set; } = "[]";
    public string GeographiesJson { get; set; } = "[]";
    public string Timing { get; set; } = string.Empty;
    public long? BudgetMinor { get; set; }
    public bool BudgetUnknown { get; set; }
    public string? Currency { get; set; }
    public string? VatStatus { get; set; }
    public long? FeesMinor { get; set; }
    public string ConstraintsJson { get; set; } = "[]";
    public string MeasurementJson { get; set; } = "[]";
    public string FactsJson { get; set; } = "[]";
    public string UnknownsJson { get; set; } = "[]";
    public string AssumptionsJson { get; set; } = "[]";
    public string ConflictsJson { get; set; } = "[]";
    public Guid[] EvidenceItemIds { get; set; } = [];
    public string Status { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? SubmittedBy { get; set; }
    public Guid? ApprovedBy { get; set; }
    public Guid? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }
    public string? RequestedChanges { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

internal static class BriefRowMapper
{
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static CampaignBriefSummaryView ToView(this CampaignBriefRow row) => new(
        row.Id, row.TenantId, row.ClientId, row.ClientName, row.OpportunityId, row.Title,
        row.OwnerUserId, row.Status, row.CurrentDraftVersionId, row.ReadyVersionId,
        row.ApprovedVersionId, row.Version, row.UpdatedAtUtc);

    public static BriefSourceView ToView(this BriefSourceRow row) => new(
        row.Id, row.SourceType, row.Locator, row.Title, row.Content, row.ContentHash,
        row.CreatedBy, row.CreatedAtUtc);

    public static BriefVersionView ToView(this BriefVersionRow row) => new(
        row.Id, row.BriefId, row.BaseVersionId, row.SourceId, row.VersionNumber,
        row.BusinessProblem, row.Objective, Read<string>(row.AudiencesJson),
        Read<string>(row.GeographiesJson), row.Timing, row.BudgetMinor, row.BudgetUnknown,
        row.Currency, row.VatStatus, row.FeesMinor, Read<string>(row.ConstraintsJson),
        Read<string>(row.MeasurementJson), Read<string>(row.FactsJson),
        Read<BriefUnknownInput>(row.UnknownsJson),
        Read<BriefAssumptionInput>(row.AssumptionsJson),
        Read<BriefConflictInput>(row.ConflictsJson), row.EvidenceItemIds, row.Status,
        row.CreatedBy, row.SubmittedBy, row.ApprovedBy, row.RejectedBy,
        row.RejectionReason, row.RequestedChanges, row.Version, row.CreatedAtUtc);

    private static T[] Read<T>(string value) =>
        JsonSerializer.Deserialize<T[]>(value, StoredJson) ?? [];
}
