using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingCommands
{
    private async Task<CommandOutcome> SubmitPurchaseOrderOutcomeAsync(
        CommandEnvelope<SubmitPurchaseOrderCommand> envelope,
        CancellationToken cancellationToken)
    {
        var source = await SelectedSourceAsync(
            envelope.TenantId, envelope.Command.ProposalVersionId,
            envelope.Command.ProposalOptionId, cancellationToken);
        var currency = FundingDocumentPolicy.Currency(envelope.Command.Currency);
        if (envelope.Command.AmountMinor != source.TotalMinor || currency != source.Currency)
            throw new FundingReviewRequiredException();
        var number = FundingDocumentPolicy.PurchaseOrderNumber(
            envelope.Command.PurchaseOrderNumber);
        var document = await ProtectAsync(envelope.Command.Document, cancellationToken);
        var id = Guid.NewGuid();
        var objectKey = ObjectKey(envelope.TenantId.Value, "purchase-orders", id, document.Sha256);
        var now = timeProvider.GetUtcNow();
        var changed = await store.InsertPurchaseOrderAsync(
            id, envelope, source, number, document, objectKey, now, cancellationToken);
        if (changed != 1) throw new InvalidLifecycleTransitionException();
        await objectStore.PutAsync(
            objectKey, document.Content, document.MediaType, cancellationToken);
        var view = (await store.FindPurchaseOrderAsync(id, false, cancellationToken)
            ?? throw new InvalidOperationException("The purchase order was not persisted.")).ToView();
        return Outcome(
            envelope, view, id, MasterDataReferences.CommercialResourceTypes.PurchaseOrder,
            MasterDataReferences.CommercialActions.PurchaseOrderSubmitted,
            MasterDataReferences.CommercialEventTypes.PurchaseOrderSubmitted, now);
    }

    private async Task<CommandOutcome> ApprovePurchaseOrderOutcomeAsync(
        Guid id,
        CommandEnvelope<ApprovePurchaseOrderCommand> envelope,
        CancellationToken cancellationToken)
    {
        var row = await store.FindPurchaseOrderAsync(id, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Purchase order access denied.");
        if (row.Status != MasterDataCodes.LifecycleStatuses.Submitted)
            throw new InvalidLifecycleTransitionException();
        if (row.SubmittedBy == envelope.ActorId.Value)
            throw new UnauthorizedAccessException("A different reviewer must approve the purchase order.");
        var source = await SelectedSourceAsync(
            envelope.TenantId, row.ProposalVersionId, row.ProposalOptionId, cancellationToken);
        if (row.AmountMinor != source.TotalMinor || row.Currency != source.Currency)
            throw new FundingReviewRequiredException();
        var reason = FundingDocumentPolicy.Reason(envelope.Command.ReconciliationReason);
        var now = timeProvider.GetUtcNow();
        await store.ApprovePurchaseOrderAsync(row, envelope, reason, now, cancellationToken);
        var view = (await store.FindPurchaseOrderAsync(id, false, cancellationToken))!.ToView();
        return Outcome(
            envelope, view, id, MasterDataReferences.CommercialResourceTypes.PurchaseOrder,
            MasterDataReferences.CommercialActions.PurchaseOrderApproved,
            MasterDataReferences.CommercialEventTypes.PurchaseOrderApproved, now);
    }

    private async Task<FundingSourceRow> SelectedSourceAsync(
        Advertified.Commercial.Domain.Governance.TenantId tenantId,
        Guid proposalVersionId,
        Guid proposalOptionId,
        CancellationToken cancellationToken) =>
        await store.FindSelectedSourceAsync(
            tenantId, proposalVersionId, proposalOptionId, cancellationToken)
        ?? throw new FundingReviewRequiredException();

    private async Task<PreparedFundingDocument> ProtectAsync(
        FundingDocument upload,
        CancellationToken cancellationToken)
    {
        var document = FundingDocumentPolicy.Prepare(upload);
        var scan = await malwareScanner.ScanAsync(document.Content, cancellationToken);
        if (!scan.IsClean) throw new UnsafeInventorySourceException();
        return document;
    }

    private static string ObjectKey(Guid tenantId, string family, Guid id, string hash) =>
        $"protected/{tenantId:N}/funding/{family}/{id:N}/{hash}";
}
