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
    public async Task<MeasurementReportPage> ListAsync(
        ActorId actorId, TenantId tenantId, int pageSize, Guid? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListSummariesAsync(tenantId, pageSize + 1, cursor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var items = rows.Take(pageSize).ToArray();
        return new(items, rows.Count > pageSize ? items[^1].Id : null);
    }

    public async Task<MeasurementCampaignPage> ListCampaignsAsync(
        ActorId actorId, TenantId tenantId, int pageSize, Guid? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 100);
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var rows = await store.ListCampaignSummariesAsync(
            tenantId, pageSize + 1, cursor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var items = rows.Take(pageSize).ToArray();
        return new(items, rows.Count > pageSize ? items[^1].Id : null);
    }

    public async Task<MeasurementReportView> GetAsync(
        ActorId actorId,
        TenantId tenantId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        await EnsureAllowedAsync(actorId, tenantId, cancellationToken);
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var view = await store.GetViewAsync(reportId, evidenceStore, cancellationToken)
            ?? throw new UnauthorizedAccessException("Measurement report access denied.");
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    private async Task EnsureAllowedAsync(
        ActorId actorId, TenantId tenantId, CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.MeasurementReportView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Measurement report access denied.");
    }
}
