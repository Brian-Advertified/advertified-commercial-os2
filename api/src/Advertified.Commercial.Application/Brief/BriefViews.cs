using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Brief;

public sealed record CampaignBriefSummaryView(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string ClientName,
    Guid? OpportunityId,
    string Title,
    Guid OwnerUserId,
    string Status,
    Guid? CurrentDraftVersionId,
    Guid? ReadyVersionId,
    Guid? ApprovedVersionId,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record BriefSourceView(
    Guid Id,
    string SourceType,
    string Locator,
    string Title,
    string Content,
    string ContentHash,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc);

public sealed record BriefSpatialRequirementView(
    Guid Id,
    string Type,
    string Priority,
    string Label,
    string GeoJson,
    decimal? RadiusMetres,
    decimal? CoverageThreshold,
    bool BufferInferred,
    string? BoundarySource,
    string? BoundaryVersion,
    string SourceLocator,
    bool IsVerified);

public sealed record BriefVersionView(
    Guid Id,
    Guid BriefId,
    Guid? BaseVersionId,
    Guid SourceId,
    int VersionNumber,
    string BusinessProblem,
    string Objective,
    IReadOnlyList<string> Audiences,
    IReadOnlyList<string> Geographies,
    string Timing,
    long? BudgetMinor,
    bool BudgetUnknown,
    string? Currency,
    string? VatStatus,
    long? FeesMinor,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Measurement,
    IReadOnlyList<string> Facts,
    IReadOnlyList<BriefUnknownInput> Unknowns,
    IReadOnlyList<BriefAssumptionInput> Assumptions,
    IReadOnlyList<BriefConflictInput> Conflicts,
    IReadOnlyList<Guid> EvidenceItemIds,
    string Status,
    Guid CreatedBy,
    Guid? SubmittedBy,
    Guid? ApprovedBy,
    string? ApprovalMode,
    Guid? RejectedBy,
    string? RejectionReason,
    string? RequestedChanges,
    long Version,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BriefSpatialRequirementView>? SpatialRequirements = null);

public sealed record CampaignBriefView(
    CampaignBriefSummaryView Brief,
    IReadOnlyList<BriefSourceView> Sources,
    IReadOnlyList<BriefVersionView> Versions);

public interface IBriefReader
{
    Task<IReadOnlyList<CampaignBriefSummaryView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<CampaignBriefView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken);
}
