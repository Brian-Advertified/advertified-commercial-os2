using Advertified.Commercial.Application.Campaign;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Campaign;

public sealed class CampaignReader(
    CampaignRecordStore store,
    ITenantAuthorizer authorizer) : ICampaignReader
{
    public async Task<IReadOnlyList<CampaignView>> ListAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => row.ToView()).ToArray();
    }

    public async Task<CampaignView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindAsync(campaignId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Campaign access denied.");
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.CampaignView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Campaign access denied.");
    }
}
