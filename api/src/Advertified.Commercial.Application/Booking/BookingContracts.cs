using System.Text.Json.Serialization;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Booking;

public sealed record CreateBookingCommand(
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid MediaPlanLineId,
    string Terms);

public sealed record RequestBookingConfirmationCommand(string Reason);

public sealed record ConfirmBookingCommand(
    bool AcceptTerms,
    string Reason,
    string? Note);

public sealed record BookablePlanLineView(
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    Guid PlanVersionId,
    Guid MediaPlanLineId,
    string SupplierName,
    string ProductName,
    string Channel,
    string Geography,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    int RunningPeriods,
    int Quantity,
    long ClientPriceMinor,
    long FeesMinor,
    long VatMinor,
    string Currency,
    bool AlreadyBooked);

public sealed record BookingView(
    Guid Id,
    Guid BuyerTenantId,
    Guid SupplierTenantId,
    Guid? ProposalVersionId,
    Guid? ProposalOptionId,
    Guid? ProposalDecisionId,
    Guid? PlanVersionId,
    Guid? MediaPlanLineId,
    Guid MarketplaceListingVersionId,
    string SupplierName,
    string ProductName,
    string Channel,
    string Geography,
    DateOnly FlightStart,
    DateOnly FlightEnd,
    int RunningPeriods,
    int Quantity,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    long? SupplierCostMinor,
    long? ClientPriceMinor,
    long? FeesMinor,
    long? VatMinor,
    string Currency,
    string Terms,
    string Status,
    Guid CreatedBy,
    DateTimeOffset CreatedAtUtc,
    Guid? RequestedBy,
    DateTimeOffset? RequestedAtUtc,
    string? RequestReason,
    Guid? ConfirmedBy,
    DateTimeOffset? ConfirmedAtUtc,
    string? ConfirmationReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SupplierNote,
    bool TermsAccepted,
    long Version,
    DateTimeOffset UpdatedAtUtc,
    InventorySupplierCommercialValues? SupplierCommercial = null,
    InventoryCommercialTermsValues? CommercialTerms = null,
    InventoryDeliverableValues? Deliverable = null,
    InventorySpatialValues? Spatial = null,
    string? VatTreatment = null,
    Guid? LogoAssetId = null);

public interface IBookingCommands
{
    Task<CommandResult<BookingView>> CreateAsync(
        CommandEnvelope<CreateBookingCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BookingView>> RequestConfirmationAsync(
        Guid bookingId,
        CommandEnvelope<RequestBookingConfirmationCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<BookingView>> ConfirmAsync(
        Guid bookingId,
        CommandEnvelope<ConfirmBookingCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IBookingReader
{
    Task<IReadOnlyList<BookablePlanLineView>> ListBookableLinesAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<BookingView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid bookingId,
        CancellationToken cancellationToken);
}

public sealed class BookingReviewRequiredException : Exception;
