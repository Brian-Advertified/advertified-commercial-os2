using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class PerformanceEvidenceReader(
    PerformanceEvidenceRecordStore store,
    ITenantAuthorizer authorizer) : IPerformanceEvidenceReader
{
    public async Task<PerformanceEvidenceView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid evidenceId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.PerformanceFactView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Performance evidence access denied.");
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var view = await store.GetViewAsync(evidenceId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Performance evidence access denied.");
        await transaction.CommitAsync(cancellationToken);
        return view;
    }
}
