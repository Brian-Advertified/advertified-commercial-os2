using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Delivery;

public sealed record DeliveryProofFileUpload(
    string FileName,
    string MediaType,
    byte[] Content);

public sealed record SubmitDeliveryProofCommand(
    Guid BookingId,
    string ProofType,
    DateTimeOffset CapturedAtUtc,
    string LocationDescription,
    decimal? Latitude,
    decimal? Longitude,
    string SourceReference,
    string Reason,
    DeliveryProofFileUpload File);

public sealed record ReviewDeliveryProofCommand(bool Approved, string Reason);

public sealed record DeliveryProofRequestView(
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
    string? LatestProofStatus);

public sealed record DeliveryProofView(
    Guid Id,
    Guid CampaignId,
    Guid BookingId,
    Guid SupplierTenantId,
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
    DateTimeOffset UpdatedAtUtc);

public interface IDeliveryProofCommands
{
    Task<CommandResult<DeliveryProofView>> SubmitAsync(
        Guid campaignId,
        CommandEnvelope<SubmitDeliveryProofCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<DeliveryProofView>> ReviewAsync(
        Guid campaignId,
        Guid proofId,
        CommandEnvelope<ReviewDeliveryProofCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IDeliveryProofReader
{
    Task<IReadOnlyList<DeliveryProofRequestView>> ListRequestsAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<DeliveryProofView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proofId,
        CancellationToken cancellationToken);
}

public sealed class DeliveryProofBlockedException : Exception;
public sealed class DeliveryProofFileRejectedException : Exception;
