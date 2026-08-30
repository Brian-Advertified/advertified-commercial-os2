using Advertified.Commercial.Application.Delivery;

namespace Advertified.Commercial.Infrastructure.Delivery;

internal sealed record DeliveryProofSourceRow(
    Guid BuyerTenantId,
    Guid SupplierTenantId,
    Guid CampaignId,
    Guid BookingId,
    Guid CampaignOwnerUserId,
    long CampaignVersion,
    DateOnly FlightStart,
    DateOnly FlightEnd);

internal sealed record DeliveryProofRow(
    Guid Id,
    Guid BuyerTenantId,
    Guid SupplierTenantId,
    Guid CampaignId,
    Guid BookingId,
    string ProofType,
    string FileName,
    string MediaType,
    long SizeBytes,
    string ContentSha256,
    bool SignatureValidated,
    string MalwareScanStatus,
    DateTimeOffset CapturedAtUtc,
    string LocationDescription,
    decimal? Latitude,
    decimal? Longitude,
    string SourceReference,
    string SubmissionReason,
    string Status,
    Guid SubmittedBy,
    Guid SubmitterTenantId,
    DateTimeOffset SubmittedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    string? ReviewReason,
    long Version,
    DateTimeOffset UpdatedAtUtc)
{
    internal DeliveryProofView ToView() => new(
        Id, CampaignId, BookingId, SupplierTenantId, ProofType, FileName, MediaType,
        SizeBytes, ContentSha256, SignatureValidated, MalwareScanStatus, CapturedAtUtc,
        LocationDescription, Latitude, Longitude, SourceReference, SubmissionReason,
        Status, SubmittedBy, SubmitterTenantId, SubmittedAtUtc, ReviewedBy,
        ReviewedAtUtc, ReviewReason, Version, UpdatedAtUtc);
}

internal sealed record PreparedDeliveryProof(
    string ProofType,
    string FileName,
    string MediaType,
    byte[] Content,
    string Sha256,
    DateTimeOffset CapturedAtUtc,
    string LocationDescription,
    decimal? Latitude,
    decimal? Longitude,
    string SourceReference,
    string Reason);
