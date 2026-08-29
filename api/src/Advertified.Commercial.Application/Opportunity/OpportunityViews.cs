using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Opportunity;

public sealed record OpportunityView(
    Guid Id,
    Guid TenantId,
    Guid ClientId,
    string Title,
    string SourceType,
    string? SourceRef,
    Guid OwnerUserId,
    string Stage,
    long? ExpectedValueMinor,
    string? Currency,
    DateOnly? Deadline,
    string? ProblemSummary,
    string? ObjectiveSummary,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record EvidenceSourceView(
    Guid Id,
    Guid OpportunityId,
    string Type,
    string Locator,
    string Title,
    string ContentHash,
    string PolicyBasis,
    string CaptureStatus,
    long Version,
    DateTimeOffset CapturedAtUtc);

public sealed record EvidenceItemView(
    Guid Id,
    Guid SourceId,
    string Locator,
    string ClaimType,
    string OriginalValueJson,
    string? ReviewedValueJson,
    string Excerpt,
    decimal Confidence,
    string ReviewStatus,
    string? Decision,
    string? ReviewReason,
    Guid CreatedBy,
    Guid? ReviewedBy,
    long Version);

public sealed record EvidenceSetView(
    Guid Id,
    Guid OpportunityId,
    int VersionNumber,
    IReadOnlyList<Guid> EvidenceItemIds,
    IReadOnlyList<string> Gaps,
    string Status,
    Guid CreatedBy,
    Guid? ApprovedBy,
    long Version);

public sealed record BusinessInterpretationView(
    Guid Id,
    Guid OpportunityId,
    Guid EvidenceSetId,
    int VersionNumber,
    string ArtifactJson,
    string EvidenceBindingsJson,
    string UnknownsJson,
    string AssumptionsJson,
    string Status,
    Guid CreatedBy,
    Guid? ConfirmedBy,
    long Version);

public sealed record OpportunityAngleView(
    Guid Id,
    Guid AngleSetId,
    int Rank,
    string Title,
    string Rationale,
    string EvidenceItemIdsJson,
    decimal Confidence,
    string Status,
    Guid? SelectedBy,
    long Version);

public sealed record CriticObjectionView(
    Guid Id,
    string Severity,
    string FieldPath,
    string EvidenceGap,
    string RecommendedResolution,
    string? Resolution,
    string? ResolutionReason,
    Guid? ResolvedBy,
    long Version);

public sealed record StrategyVersionView(
    Guid Id,
    Guid OpportunityId,
    int VersionNumber,
    string ArtifactJson,
    string EvidenceBindingsJson,
    string UnknownsJson,
    string AssumptionsJson,
    string Status,
    Guid CreatedBy,
    Guid? SubmittedBy,
    Guid? ApprovedBy,
    Guid? RejectedBy,
    string? RejectionReason,
    long Version,
    IReadOnlyList<CriticObjectionView> Objections);

public sealed record AgentRunView(
    Guid Id,
    Guid OpportunityId,
    string RunKind,
    string Status,
    string? CurrentStep,
    int Attempts,
    string? ErrorCode,
    string? RecoveryAction,
    long IncrementalCostMinor,
    long Version,
    DateTimeOffset UpdatedAtUtc);

public sealed record HumanTaskView(
    Guid Id,
    Guid? OpportunityId,
    Guid? BriefId,
    string TaskType,
    string Status,
    string Title,
    string WhyItMatters,
    string ResourceType,
    Guid ResourceId,
    long ResourceVersion,
    Guid AssigneeUserId,
    long Version,
    DateTimeOffset CreatedAtUtc);

public sealed record HumanTaskCompletionView(
    Guid TaskId,
    string TaskType,
    string Status,
    Guid ResourceId,
    long ResourceVersion);

public sealed record OpportunityDetailView(
    OpportunityView Opportunity,
    IReadOnlyList<EvidenceSourceView> Sources,
    IReadOnlyList<EvidenceItemView> EvidenceItems,
    EvidenceSetView? EvidenceSet,
    BusinessInterpretationView? Interpretation,
    IReadOnlyList<OpportunityAngleView> Angles,
    StrategyVersionView? Strategy,
    Guid? BriefId,
    IReadOnlyList<AgentRunView> Runs,
    string NextAction);

public interface IOpportunityReader
{
    Task<CursorPage<OpportunityView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<OpportunityDetailView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid opportunityId,
        CancellationToken cancellationToken);

    Task<StrategyVersionView> GetStrategyAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid strategyId,
        CancellationToken cancellationToken);

    Task<AgentRunView> GetRunAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid runId,
        CancellationToken cancellationToken);

    Task<CursorPage<HumanTaskView>> ListTasksAsync(
        ActorId actorId,
        TenantId tenantId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);

    Task<HumanTaskView> GetTaskAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid taskId,
        CancellationToken cancellationToken);
}
