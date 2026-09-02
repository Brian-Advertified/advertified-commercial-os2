using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Planning;

public sealed record GenerateAudiencesCommand;

public sealed record GenerateMediaMixCommand;

public sealed record MediaRunningPeriodInput(
    DateOnly Start,
    DateOnly End);

public sealed record MediaAllocationInput(
    string Channel,
    long BudgetMinor,
    string Role,
    IReadOnlyList<MediaRunningPeriodInput> RunningPeriods);

public sealed record UpdateMediaMixCommand(
    IReadOnlyList<MediaAllocationInput> Allocations,
    string? Reason);

public sealed record ApproveMediaMixCommand(string? Reason);

public sealed record GenerateShortlistCommand;

public sealed record SelectShortlistCommand(
    IReadOnlyList<Guid> SelectedCandidateIds,
    string? Reason);

public sealed record GenerateMediaPlanCommand;

public sealed record ResolvePlanObjectionCommand(
    string Resolution,
    string Reason);

public sealed record ApproveMediaPlanCommand(string? Reason);

public interface IPlanningCommands
{
    Task<CommandResult<CampaignModeSelectionView>> SelectCampaignModeAsync(
        Guid briefVersionId,
        CommandEnvelope<SelectCampaignModeCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<AudienceDefinitionSetView>> GenerateAudiencesAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateAudiencesCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaMixVersionView>> GenerateMediaMixAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaMixCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaMixVersionView>> UpdateMediaMixAsync(
        Guid mixVersionId,
        CommandEnvelope<UpdateMediaMixCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaMixVersionView>> ApproveMediaMixAsync(
        Guid mixVersionId,
        CommandEnvelope<ApproveMediaMixCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryShortlistVersionView>> GenerateShortlistAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateShortlistCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InventoryShortlistVersionView>> SelectShortlistAsync(
        Guid shortlistVersionId,
        CommandEnvelope<SelectShortlistCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaPlanVersionView>> GenerateMediaPlanAsync(
        Guid briefVersionId,
        CommandEnvelope<GenerateMediaPlanCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaPlanVersionView>> ResolvePlanObjectionAsync(
        Guid planVersionId,
        string objectionCode,
        CommandEnvelope<ResolvePlanObjectionCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<MediaPlanVersionView>> ApproveMediaPlanAsync(
        Guid planVersionId,
        CommandEnvelope<ApproveMediaPlanCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IPlanningReader
{
    Task<IReadOnlyList<PlanningSummaryView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<PlanningWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefVersionId,
        CancellationToken cancellationToken);

    Task<MediaPlanVersionView> GetPlanAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid planVersionId,
        CancellationToken cancellationToken);
}

public sealed class PlanningInputStaleException : Exception
{
    public PlanningInputStaleException() : base("A planning input changed.")
    {
    }
}

public sealed class PlanningApprovalBlockedException : Exception
{
    public PlanningApprovalBlockedException() : base("The planning artefact is not approvable.")
    {
    }
}

public sealed class SupplyConfirmationRequiredException : Exception
{
    public SupplyConfirmationRequiredException()
        : base("Selected inventory requires current supplier confirmation.")
    {
    }
}
