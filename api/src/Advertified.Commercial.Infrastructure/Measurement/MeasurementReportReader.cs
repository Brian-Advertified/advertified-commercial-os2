using Advertified.Commercial.Application.Measurement;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Measurement;

public sealed class MeasurementReportReader(
    MeasurementReportRecordStore store,
    PerformanceEvidenceRecordStore evidenceStore,
    ITenantAuthorizer authorizer) : IMeasurementReportReader
{
    public async Task<MeasurementReportView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.MeasurementReportView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Measurement report access denied.");
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var view = await store.GetViewAsync(reportId, evidenceStore, cancellationToken)
            ?? throw new UnauthorizedAccessException("Measurement report access denied.");
        await transaction.CommitAsync(cancellationToken);
        return view;
    }
}
