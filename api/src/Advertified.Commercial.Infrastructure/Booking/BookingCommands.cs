using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.CommercialSettings;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed partial class BookingCommands(
    BookingRecordStore store,
    CommercialPolicyRecordStore policyStore,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IBookingCommands
{
    public Task<CommandResult<BookingView>> CreateAsync(
        CommandEnvelope<CreateBookingCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.BookingCreate,
            token => CreateOutcomeAsync(envelope, token), cancellationToken);

    public Task<CommandResult<BookingView>> RequestConfirmationAsync(
        Guid bookingId,
        CommandEnvelope<RequestBookingConfirmationCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.BookingRequest,
            token => RequestOutcomeAsync(bookingId, envelope, token), cancellationToken);

    public Task<CommandResult<BookingView>> ConfirmAsync(
        Guid bookingId,
        CommandEnvelope<ConfirmBookingCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync(
            envelope, MasterDataReferences.Permissions.BookingConfirm,
            token => ConfirmOutcomeAsync(bookingId, envelope, token), cancellationToken);

    private async Task<CommandResult<BookingView>> DispatchAsync<TCommand>(
        CommandEnvelope<TCommand> envelope,
        PermissionCode permission,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, permission, execute, cancellationToken);
        return CommandOutcomeFactory.ToResult<BookingView>(receipt);
    }
}
