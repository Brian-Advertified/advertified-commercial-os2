using Advertified.Commercial.Application.Brief;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Constants;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Infrastructure.Brief;

public sealed class BriefReader(
    BriefRecordStore store,
    ITenantAuthorizer authorizer) : IBriefReader
{
    public async Task<CampaignBriefView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid briefId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.BriefView, cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Brief access denied.");
        }

        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        if (!await store.CanAccessAsync(tenantId, actorId.Value, briefId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Brief access denied.");
        }

        var brief = await store.FindBriefAsync(tenantId, briefId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Brief access denied.");
        var sources = await store.ListSourcesAsync(tenantId, briefId, cancellationToken);
        var versions = await store.ListVersionsAsync(tenantId, briefId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CampaignBriefView(
            brief.ToView(),
            sources.Select(BriefRowMapper.ToView).ToArray(),
            versions.Select(BriefRowMapper.ToView).ToArray());
    }
}
