using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Marketplace;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Inventory;

namespace Advertified.Commercial.Infrastructure.Marketplace;

public sealed partial class MarketplaceCommands(
    MarketplaceRecordStore store,
    CommandDispatcher dispatcher,
    InventorySupplierAccessPolicy supplierAccess,
    TimeProvider timeProvider) : IMarketplaceCommands
{
    public Task<CommandResult<MarketplaceListingView>> CreateListingAsync(
        CommandEnvelope<CreateMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<CreateMarketplaceListingCommand, MarketplaceListingView>(
            envelope, MasterDataReferences.Permissions.SupplierInventoryManage,
            token => CreateListingOutcomeAsync(envelope, token), cancellationToken,
            token => supplierAccess.EnsureProductAccessAsync(
                envelope.ActorId, envelope.TenantId, envelope.Command.ProductId, token));

    public Task<CommandResult<MarketplaceListingView>> PublishListingAsync(
        Guid listingId, CommandEnvelope<PublishMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<PublishMarketplaceListingCommand, MarketplaceListingView>(
            envelope, MasterDataReferences.Permissions.SupplierInventoryManage,
            token => PublishListingOutcomeAsync(listingId, envelope, token), cancellationToken,
            token => EnsureListingAccessAsync(envelope.ActorId, envelope.TenantId, listingId, token));

    public Task<CommandResult<MarketplaceListingView>> ArchiveListingAsync(
        Guid listingId, CommandEnvelope<ArchiveMarketplaceListingCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<ArchiveMarketplaceListingCommand, MarketplaceListingView>(
            envelope, MasterDataReferences.Permissions.SupplierInventoryManage,
            token => ArchiveListingOutcomeAsync(listingId, envelope, token), cancellationToken,
            token => EnsureListingAccessAsync(envelope.ActorId, envelope.TenantId, listingId, token));

    public Task<CommandResult<MarketplaceRfqView>> CreateRfqAsync(
        CommandEnvelope<CreateMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<CreateMarketplaceRfqCommand, MarketplaceRfqView>(
            envelope, MasterDataReferences.Permissions.RfqCreate,
            token => CreateRfqOutcomeAsync(envelope, token), cancellationToken);

    public Task<CommandResult<MarketplaceRfqView>> SendRfqAsync(
        Guid rfqId, CommandEnvelope<SendMarketplaceRfqCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<SendMarketplaceRfqCommand, MarketplaceRfqView>(
            envelope, MasterDataReferences.Permissions.RfqSend,
            token => SendRfqOutcomeAsync(rfqId, envelope, token), cancellationToken);

    public Task<CommandResult<MarketplaceRfqView>> SubmitResponseAsync(
        Guid rfqId, CommandEnvelope<SubmitMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<SubmitMarketplaceResponseCommand, MarketplaceRfqView>(
            envelope, MasterDataReferences.Permissions.RfqRespond,
            token => SubmitResponseOutcomeAsync(rfqId, envelope, token), cancellationToken);

    public Task<CommandResult<MarketplaceRfqView>> AcceptResponseAsync(
        Guid responseId, CommandEnvelope<AcceptMarketplaceResponseCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<AcceptMarketplaceResponseCommand, MarketplaceRfqView>(
            envelope, MasterDataReferences.Permissions.RfqReview,
            token => AcceptResponseOutcomeAsync(responseId, envelope, token), cancellationToken);

    private async Task<CommandResult<TView>> DispatchAsync<TCommand, TView>(
        CommandEnvelope<TCommand> envelope, PermissionCode permission,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? authorizeResource = null)
        where TCommand : notnull where TView : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, permission, execute, cancellationToken, authorizeResource);
        return CommandOutcomeFactory.ToResult<TView>(receipt);
    }

    private async Task EnsureListingAccessAsync(
        ActorId actorId, TenantId tenantId, Guid listingId, CancellationToken cancellationToken)
    {
        var listing = await store.FindListingAsync(listingId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Marketplace listing access denied.");
        if (listing.SupplierTenantId != tenantId.Value)
            throw new UnauthorizedAccessException("Marketplace listing access denied.");
        await supplierAccess.EnsureProductAccessAsync(actorId, tenantId, listing.ProductId, cancellationToken);
    }
}
