using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Campaign;

internal sealed record CampaignSourceRow(
    Guid BriefId,
    Guid BriefVersionId,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    Guid PaymentIntentId,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid OwnerUserId,
    string MeasurementPlanJson,
    int RequiredBookingCount);

internal sealed record CampaignRow(
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
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid? BookingsConfirmedBy,
    DateTimeOffset? BookingsConfirmedAtUtc,
    string? BookingConfirmationReason,
    long Version,
    DateTimeOffset UpdatedAtUtc)
{
    internal CampaignView ToView()
    {
        var nextAction = Status == MasterDataCodes.LifecycleStatuses.Planned &&
            RequiredBookingCount > 0 && ConfirmedBookingCount == RequiredBookingCount
                ? MasterDataCodes.Permissions.CampaignConfirmBookings
                : null;
        return new(
            Id, BriefId, BriefVersionId, ProposalVersionId, ProposalOptionId,
            ProposalDecisionId, PlanVersionId, PaymentIntentId, FundingStatus,
            Title, StartDate, EndDate, OwnerUserId, MeasurementPlanJson, Status,
            RequiredBookingCount, ConfirmedBookingCount, nextAction, CreatedBy,
            CreatedAtUtc, BookingsConfirmedBy, BookingsConfirmedAtUtc,
            BookingConfirmationReason, Version, UpdatedAtUtc);
    }
}
