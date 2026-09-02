using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Brief;

public sealed record CreateBriefCommand(
    string Title,
    Guid OwnerUserId,
    string SourceLocator,
    string SourceTitle,
    string SourceContent,
    Guid? ClientId = null,
    string? ClientName = null,
    string? SourceType = null);

public sealed record BriefUnknownInput(
    string FieldPath,
    string Question,
    bool IsBlocking);

public sealed record BriefAssumptionInput(
    string FieldPath,
    string Value,
    string Impact,
    string ValidationNeeded);

public sealed record BriefConflictInput(
    string FieldPath,
    string Description,
    string Severity,
    bool Resolved,
    string? Resolution);

public sealed record BriefSpatialRequirementInput(
    string Type,
    string Priority,
    string Label,
    string GeoJson,
    decimal? RadiusMetres = null,
    decimal? CoverageThreshold = null,
    string? BoundarySource = null,
    string? BoundaryVersion = null,
    string? SourceLocator = null,
    bool IsVerified = false);

public sealed record CreateBriefVersionCommand(
    Guid BriefId,
    Guid? BaseVersionId,
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
    IReadOnlyList<BriefSpatialRequirementInput>? SpatialRequirements = null);

public sealed record SubmitBriefVersionCommand(
    Guid? ConfirmerUserId,
    string? Comment);

public sealed record MarkBriefVersionReadyCommand;

public sealed record ApproveBriefVersionCommand(string? Reason);

public sealed record RejectBriefVersionCommand(
    string Reason,
    string RequestedChanges);

public interface IBriefCommands
{
    Task<CommandResult<CampaignBriefSummaryView>> CreateAsync(
        CommandEnvelope<CreateBriefCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BriefVersionView>> CreateVersionAsync(
        Guid briefId,
        CommandEnvelope<CreateBriefVersionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BriefVersionView>> SubmitAsync(
        Guid versionId,
        CommandEnvelope<SubmitBriefVersionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BriefVersionView>> MarkReadyAsync(
        Guid versionId,
        CommandEnvelope<MarkBriefVersionReadyCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BriefVersionView>> ApproveAsync(
        Guid versionId,
        CommandEnvelope<ApproveBriefVersionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BriefVersionView>> RejectAsync(
        Guid versionId,
        CommandEnvelope<RejectBriefVersionCommand> envelope,
        CancellationToken cancellationToken);
}
