using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Security;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed class FundingReader(
    FundingRecordStore store,
    ITenantAuthorizer authorizer) : IFundingReader
{
    public async Task<FundingWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var decision = await authorizer.AuthorizeAsync(
            actorId, tenantId, MasterDataReferences.Permissions.FundingView,
            cancellationToken);
        if (!decision.IsAllowed)
            throw new UnauthorizedAccessException("Funding access denied.");
        await using var transaction = await store.BeginSessionAsync(
            actorId, tenantId, cancellationToken);
        var orders = await store.ListPurchaseOrdersAsync(cancellationToken);
        var invoices = await store.ListInvoicesAsync(cancellationToken);
        var payments = await store.ListPaymentsAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FundingWorkspaceView(
            orders.Select(row => row.ToView()).ToArray(),
            invoices.Select(row => row.ToView()).ToArray(),
            payments.Select(row => row.ToView()).ToArray());
    }
}
