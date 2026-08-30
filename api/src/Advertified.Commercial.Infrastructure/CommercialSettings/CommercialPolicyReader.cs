using Advertified.Commercial.Application.CommercialSettings;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.CommercialSettings;

public sealed class CommercialPolicyReader(
    CommercialPolicyRecordStore store,
    ITenantAuthorizer authorizer) : ICommercialPolicyReader
{
    public async Task<CommercialPolicyView> GetCurrentAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId,
            tenantId,
            MasterDataReferences.Permissions.CommercialSettingsView,
            cancellationToken);
        if (!decision.IsAllowed)
        {
            throw new UnauthorizedAccessException("Commercial policy access denied.");
        }
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindCurrentAsync(tenantId, cancellationToken)
            ?? throw new CommercialPolicyNotConfiguredException();
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }
}
