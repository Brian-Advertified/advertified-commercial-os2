using Advertified.Commercial.Application.Booking;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}/bookings")
            .WithTags("Bookings").RequireAuthorization();
        group.MapGet(string.Empty, ListAsync)
            .WithName("ListBookings").Produces<IReadOnlyList<BookingView>>()
            .WithQueryProblems();
        group.MapGet("/bookable-lines", ListBookableLinesAsync)
            .WithName("ListBookablePlanLines")
            .Produces<IReadOnlyList<BookablePlanLineView>>().WithQueryProblems();
        group.MapGet("/{bookingId:guid}", GetAsync)
            .WithName("GetBooking").Produces<BookingView>().WithQueryProblems();
        group.MapPost(string.Empty, CreateAsync)
            .WithName("CreateBooking").Produces<BookingView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/{bookingId:guid}:request-confirmation", RequestConfirmationAsync)
            .WithName("RequestBookingConfirmation").Produces<BookingView>()
            .WithCommandProblems(requiresVersion: true);
        group.MapPost("/{bookingId:guid}:confirm", ConfirmAsync)
            .WithName("ConfirmBooking").Produces<BookingView>()
            .WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IBookingReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> ListBookableLinesAsync(
        Guid tenantId,
        ICurrentIdentity identity,
        IBookingReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListBookableLinesAsync(
            identity.ActorId, new TenantId(tenantId), cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid tenantId,
        Guid bookingId,
        HttpContext context,
        ICurrentIdentity identity,
        IBookingReader reader,
        CancellationToken cancellationToken)
    {
        var view = await reader.GetAsync(
            identity.ActorId, new TenantId(tenantId), bookingId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, view.Version);
        return Results.Ok(view);
    }

    private static async Task<IResult> CreateAsync(
        Guid tenantId,
        CreateBookingCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBookingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId, command, context, identity, commands.CreateAsync,
            clock, false, cancellationToken);
        return Results.Created(
            $"/api/v1/tenants/{tenantId}/bookings/{result.Data.Id}", result.Data);
    }

    private static async Task<IResult> RequestConfirmationAsync(
        Guid tenantId,
        Guid bookingId,
        RequestBookingConfirmationCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBookingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId, command, context, identity,
            (envelope, token) => commands.RequestConfirmationAsync(
                bookingId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> ConfirmAsync(
        Guid tenantId,
        Guid bookingId,
        ConfirmBookingCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        IBookingCommands commands,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId, command, context, identity,
            (envelope, token) => commands.ConfirmAsync(bookingId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static Task<CommandResult<BookingView>> ExecuteAsync<TCommand>(
        Guid tenantId,
        TCommand command,
        HttpContext context,
        ICurrentIdentity identity,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<BookingView>>> action,
        TimeProvider clock,
        bool requireVersion,
        CancellationToken cancellationToken)
        where TCommand : notnull =>
        CommandEndpointExecutor.ExecuteResultAsync(
            tenantId, command, context, identity, clock,
            requireVersion, action, cancellationToken);
}
