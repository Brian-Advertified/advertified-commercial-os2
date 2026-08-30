using Advertified.Commercial.Application.Delivery;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Delivery;

public sealed class DeliveryProofReader(
    DeliveryProofRecordStore store,
    ITenantAuthorizer authorizer) : IDeliveryProofReader
{
    public async Task<DeliveryProofView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid proofId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.DeliveryProofView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Delivery proof access denied.");
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var row = await store.FindAsync(proofId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Delivery proof access denied.");
        await transaction.CommitAsync(cancellationToken);
        return row.ToView();
    }
}
