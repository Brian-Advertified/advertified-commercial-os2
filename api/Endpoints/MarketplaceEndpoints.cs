using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Identity;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Api.Endpoints;

public static class MarketplaceEndpoints
{
    public static IEndpointRouteBuilder MapMarketplaceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/tenants/{tenantId:guid}")
            .WithTags("Supplier marketplace").RequireAuthorization();
        group.MapGet("/marketplace-listings", SearchListingsAsync)
            .WithName("SearchMarketplaceListings")
            .Produces<MarketplaceListingPage>().WithQueryProblems();
        group.MapGet("/marketplace-listings/{listingId:guid}", GetListingAsync)
            .WithName("GetMarketplaceListing")
            .Produces<MarketplaceListingView>().WithQueryProblems();
        group.MapPost("/marketplace-listings", CreateListingAsync)
            .WithName("CreateMarketplaceListing")
            .Produces<MarketplaceListingView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/marketplace-listings/{listingId:guid}:publish", PublishListingAsync)
            .WithName("PublishMarketplaceListing")
            .Produces<MarketplaceListingView>().WithCommandProblems(requiresVersion: true);
        group.MapPost("/marketplace-listings/{listingId:guid}:archive", ArchiveListingAsync)
            .WithName("ArchiveMarketplaceListing")
            .Produces<MarketplaceListingView>().WithCommandProblems(requiresVersion: true);
        group.MapGet("/marketplace-rfqs", ListRfqsAsync)
            .WithName("ListMarketplaceRfqs")
            .Produces<MarketplaceRfqPage>().WithQueryProblems();
        group.MapGet("/marketplace-rfqs/{rfqId:guid}", GetRfqAsync)
            .WithName("GetMarketplaceRfq")
            .Produces<MarketplaceRfqView>().WithQueryProblems();
        group.MapPost("/marketplace-rfqs", CreateRfqAsync)
            .WithName("CreateMarketplaceRfq")
            .Produces<MarketplaceRfqView>(StatusCodes.Status201Created)
            .WithCommandProblems(requiresVersion: false);
        group.MapPost("/marketplace-rfqs/{rfqId:guid}:send", SendRfqAsync)
            .WithName("SendMarketplaceRfq")
            .Produces<MarketplaceRfqView>().WithCommandProblems(requiresVersion: true);
        group.MapPost("/marketplace-rfqs/{rfqId:guid}/responses", SubmitResponseAsync)
            .WithName("SubmitMarketplaceResponse")
            .Produces<MarketplaceRfqView>().WithCommandProblems(requiresVersion: false);
        group.MapPost("/marketplace-responses/{responseId:guid}:accept", AcceptResponseAsync)
            .WithName("AcceptMarketplaceResponse")
            .Produces<MarketplaceRfqView>().WithCommandProblems(requiresVersion: true);
        return endpoints;
    }

    private static async Task<IResult> SearchListingsAsync(
        Guid tenantId, string? search, string? channel, string? geography,
        int? pageSize, string? cursor, ICurrentIdentity identity,
        IMarketplaceReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.SearchListingsAsync(
            identity.ActorId, new TenantId(tenantId),
            new MarketplaceSearchQuery(
                search, channel, geography, pageSize ?? 25, cursor),
            cancellationToken));

    private static async Task<IResult> GetListingAsync(
        Guid tenantId, Guid listingId, HttpContext context,
        ICurrentIdentity identity, IMarketplaceReader reader,
        CancellationToken cancellationToken)
    {
        var result = await reader.GetListingAsync(
            identity.ActorId, new TenantId(tenantId), listingId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateListingAsync(
        Guid tenantId, CreateMarketplaceListingCommand command, HttpContext context,
        ICurrentIdentity identity, IMarketplaceCommands commands, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId, command, context, identity, commands.CreateListingAsync,
            clock, false, cancellationToken);
        return Results.Created(
            $"/api/v1/tenants/{tenantId}/marketplace-listings/{result.Data.Id}",
            result.Data);
    }

    private static async Task<IResult> PublishListingAsync(
        Guid tenantId, Guid listingId, PublishMarketplaceListingCommand command,
        HttpContext context, ICurrentIdentity identity, IMarketplaceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(tenantId, command, context, identity,
            (envelope, token) => commands.PublishListingAsync(listingId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> ArchiveListingAsync(
        Guid tenantId, Guid listingId, ArchiveMarketplaceListingCommand command,
        HttpContext context, ICurrentIdentity identity, IMarketplaceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(tenantId, command, context, identity,
            (envelope, token) => commands.ArchiveListingAsync(listingId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> ListRfqsAsync(
        Guid tenantId, string? status, int? pageSize, string? cursor,
        ICurrentIdentity identity, IMarketplaceReader reader,
        CancellationToken cancellationToken) => Results.Ok(await reader.ListRfqsAsync(
            identity.ActorId, new TenantId(tenantId),
            new MarketplaceRfqQuery(status, pageSize ?? 25, cursor), cancellationToken));

    private static async Task<IResult> GetRfqAsync(
        Guid tenantId, Guid rfqId, HttpContext context,
        ICurrentIdentity identity, IMarketplaceReader reader,
        CancellationToken cancellationToken)
    {
        var result = await reader.GetRfqAsync(
            identity.ActorId, new TenantId(tenantId), rfqId, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateRfqAsync(
        Guid tenantId, CreateMarketplaceRfqCommand command, HttpContext context,
        ICurrentIdentity identity, IMarketplaceCommands commands, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            tenantId, command, context, identity, commands.CreateRfqAsync,
            clock, false, cancellationToken);
        return Results.Created(
            $"/api/v1/tenants/{tenantId}/marketplace-rfqs/{result.Data.Id}", result.Data);
    }

    private static async Task<IResult> SendRfqAsync(
        Guid tenantId, Guid rfqId, SendMarketplaceRfqCommand command,
        HttpContext context, ICurrentIdentity identity, IMarketplaceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(tenantId, command, context, identity,
            (envelope, token) => commands.SendRfqAsync(rfqId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> SubmitResponseAsync(
        Guid tenantId, Guid rfqId, SubmitMarketplaceResponseCommand command,
        HttpContext context, ICurrentIdentity identity, IMarketplaceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(tenantId, command, context, identity,
            (envelope, token) => commands.SubmitResponseAsync(rfqId, envelope, token),
            clock, false, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<IResult> AcceptResponseAsync(
        Guid tenantId, Guid responseId, AcceptMarketplaceResponseCommand command,
        HttpContext context, ICurrentIdentity identity, IMarketplaceCommands commands,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(tenantId, command, context, identity,
            (envelope, token) => commands.AcceptResponseAsync(responseId, envelope, token),
            clock, true, cancellationToken);
        return Results.Ok(result.Data);
    }

    private static async Task<CommandResult<TResult>> ExecuteAsync<TCommand, TResult>(
        Guid tenantId, TCommand command, HttpContext context, ICurrentIdentity identity,
        Func<CommandEnvelope<TCommand>, CancellationToken, Task<CommandResult<TResult>>> action,
        TimeProvider clock, bool requireVersion, CancellationToken cancellationToken)
        where TCommand : notnull where TResult : notnull
    {
        var envelope = CommandEnvelopeFactory.Create(
            context, new TenantId(tenantId), identity.ActorId, command, clock, requireVersion);
        var result = await action(envelope, cancellationToken);
        CommandEnvelopeFactory.SetEntityHeaders(context, result.Version, result.Replayed);
        return result;
    }
}
