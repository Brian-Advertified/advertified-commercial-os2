using Advertified.Commercial.Application.Creative;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Creative;

public sealed class CreativeReader(
    CreativeRecordStore store,
    ITenantAuthorizer authorizer) : ICreativeReader
{
    public async Task<CreativeWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var view = CreativeRecordStore.ToWorkspace(
            await store.ListWorkspaceRowsAsync(campaignId, cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<SupplierCreativeAssetView> GetSupplierAssetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var view = (await store.FindSupplierViewAsync(assetId, cancellationToken))?.ToView()
            ?? throw new UnauthorizedAccessException("Creative asset access denied.");
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.CreativeView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Creative asset access denied.");
    }
}
