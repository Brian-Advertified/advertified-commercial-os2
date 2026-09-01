using Advertified.Commercial.Application.Delivery;

namespace Advertified.Commercial.Infrastructure.Delivery;

internal sealed record DeliveryProofRequestRow(
    Guid CampaignId,
    Guid BookingId,
    string SupplierName,
    string ProductName,
    string Channel,
    string Geography,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    DateTimeOffset ProofRequestedAtUtc,
    string ProofRequestReason,
    Guid? LatestProofId,
    string? LatestProofStatus)
{
    internal DeliveryProofRequestView ToView() => new(
        CampaignId,
        BookingId,
        SupplierName,
        ProductName,
        Channel,
        Geography,
        FlightStart,
        FlightEnd,
        ProofRequestedAtUtc,
        ProofRequestReason,
        LatestProofId,
        LatestProofStatus);
}
