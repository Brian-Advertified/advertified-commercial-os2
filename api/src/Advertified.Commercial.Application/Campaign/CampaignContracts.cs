using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Campaign;

public sealed record ConfirmCampaignBookingsCommand(string Reason);
public sealed record StartCampaignCommand(string Reason);
public sealed record CompleteCampaignCommand(string CompletionReason, string ProofRequestReason);

public sealed record CampaignView(
    Guid Id,
    Guid BriefId,
    Guid BriefVersionId,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    Guid PlanVersionId,
    Guid PaymentIntentId,
    string FundingStatus,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid OwnerUserId,
    string MeasurementPlanJson,
    string Status,
    int RequiredBookingCount,
    int ConfirmedBookingCount,
    string? NextActionPermission,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid? BookingsConfirmedBy,
    DateTimeOffset? BookingsConfirmedAtUtc,
    string? BookingConfirmationReason,
    Guid? CreativeRequestedBy,
    DateTimeOffset? CreativeRequestedAtUtc,
    string? CreativeRequestReason,
    Guid? CreativeApprovedBy,
    DateTimeOffset? CreativeApprovedAtUtc,
    string? CreativeApprovalReason,
    Guid? StartedBy,
    DateTimeOffset? StartedAtUtc,
    string? StartReason,
    Guid? CompletedBy,
    DateTimeOffset? CompletedAtUtc,
    string? CompletionReason,
    Guid? ProofRequestedBy,
    DateTimeOffset? ProofRequestedAtUtc,
    string? ProofRequestReason,
    long Version,
    DateTimeOffset UpdatedAtUtc)
{
    public CreativeWorkspaceView? Creative { get; init; }
    public IReadOnlyList<DeliveryProofView> DeliveryProofs { get; init; } = [];
    public IReadOnlyList<PerformanceEvidenceView> PerformanceEvidence { get; init; } = [];
}

public interface ICampaignCommands
{
    Task<CommandResult<CampaignView>> ConfirmBookingsAsync(
        Guid campaignId,
        CommandEnvelope<ConfirmCampaignBookingsCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CampaignView>> StartAsync(
        Guid campaignId,
        CommandEnvelope<StartCampaignCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<CampaignView>> CompleteAsync(
        Guid campaignId,
        CommandEnvelope<CompleteCampaignCommand> envelope,
        CancellationToken cancellationToken);
}

public interface ICampaignReader
{
    Task<IReadOnlyList<CampaignView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<CampaignView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid campaignId,
        CancellationToken cancellationToken);
}

public sealed class CampaignReadinessBlockedException : Exception;
public sealed class CampaignDeliveryBlockedException : Exception;
