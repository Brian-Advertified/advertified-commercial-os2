using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingCommands
{
    private async Task<CommandOutcome> RequestOutcomeAsync(
        Guid bookingId,
        CommandEnvelope<RequestBookingConfirmationCommand> envelope,
        CancellationToken cancellationToken)
    {
        var booking = await LoadForTransitionAsync(
            bookingId, envelope.TenantId, true, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var policy = await policyStore.FindCurrentAsync(envelope.TenantId, cancellationToken);
        if (policy?.Id != booking.CommercialPolicyVersionId ||
            !await store.IsSupplyCurrentAsync(booking, now, cancellationToken))
        {
            throw new BookingReviewRequiredException();
        }
        var reason = BookingPolicy.RequiredReason(envelope.Command.Reason);
        await store.RequestConfirmationAsync(
            booking, envelope, reason, now, cancellationToken);
        var view = await LoadViewAsync(bookingId, cancellationToken);
        return Outcome(
            envelope, view,
            MasterDataReferences.CommercialActions.BookingConfirmationRequested,
            MasterDataReferences.CommercialEventTypes.BookingConfirmationRequested, now);
    }

    private async Task<CommandOutcome> ConfirmOutcomeAsync(
        Guid bookingId,
        CommandEnvelope<ConfirmBookingCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (!envelope.Command.AcceptTerms)
        {
            throw new ArgumentException("The supplier must accept the frozen booking terms.");
        }
        var booking = await LoadForTransitionAsync(
            bookingId, envelope.TenantId, false, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (!await store.IsSupplyCurrentAsync(booking, now, cancellationToken))
        {
            throw new BookingReviewRequiredException();
        }
        var reason = BookingPolicy.RequiredReason(envelope.Command.Reason);
        var note = BookingPolicy.OptionalNote(envelope.Command.Note);
        await store.ConfirmAsync(booking, envelope, reason, note, now, cancellationToken);
        var view = await LoadViewAsync(bookingId, cancellationToken);
        return Outcome(
            envelope, view, MasterDataReferences.CommercialActions.BookingConfirmed,
            MasterDataReferences.CommercialEventTypes.BookingConfirmed, now);
    }

    private async Task<BookingRow> LoadForTransitionAsync(
        Guid bookingId,
        TenantId tenantId,
        bool buyer,
        CancellationToken cancellationToken)
    {
        var booking = await store.FindAsync(bookingId, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Booking access denied.");
        var expectedTenant = buyer ? booking.BuyerTenantId : booking.SupplierTenantId;
        var expectedStatus = buyer
            ? MasterDataCodes.LifecycleStatuses.Draft
            : MasterDataCodes.LifecycleStatuses.PendingSupplier;
        if (expectedTenant != tenantId.Value)
        {
            throw new UnauthorizedAccessException("Booking access denied.");
        }
        if (booking.Status != expectedStatus)
        {
            throw new InvalidLifecycleTransitionException();
        }
        return booking;
    }

    private async Task<BookingView> LoadViewAsync(
        Guid bookingId,
        CancellationToken cancellationToken) =>
        (await store.FindAsync(bookingId, false, cancellationToken)
            ?? throw new InvalidOperationException("The booking was not persisted.")).ToView();

    private static CommandOutcome Outcome<TCommand>(
        CommandEnvelope<TCommand> envelope,
        BookingView view,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull =>
        CommandOutcomeFactory.Create(
            envelope, view, view.Id, view.Version,
            MasterDataReferences.CommercialResourceTypes.Booking,
            action, eventType, now);
}
