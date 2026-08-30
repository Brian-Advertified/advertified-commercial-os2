using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Campaign;

public sealed record ConfirmCampaignBookingsCommand(string Reason);

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
    long Version,
    DateTimeOffset UpdatedAtUtc)
{
    public CreativeWorkspaceView? Creative { get; init; }
}

public interface ICampaignCommands
{
    Task<CommandResult<CampaignView>> ConfirmBookingsAsync(
        Guid campaignId,
        CommandEnvelope<ConfirmCampaignBookingsCommand> envelope,
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
