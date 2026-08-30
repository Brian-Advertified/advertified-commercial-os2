using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Booking;

public sealed class BookingReader(
    BookingRecordStore store,
    ITenantAuthorizer authorizer) : IBookingReader
{
    public async Task<IReadOnlyList<BookablePlanLineView>> ListBookableLinesAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.BookingCreate,
            cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListBookableRowsAsync(tenantId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => row.ToView()).ToArray();
    }

    public async Task<IReadOnlyList<BookingView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.BookingView,
            cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListRowsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => row.ToView()).ToArray();
    }

    public async Task<BookingView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(
            actorId, tenantId, MasterDataReferences.Permissions.BookingView,
            cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindAsync(bookingId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Booking access denied.");
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        PermissionCode permission,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, permission, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Booking access denied.");
        }
    }
}
