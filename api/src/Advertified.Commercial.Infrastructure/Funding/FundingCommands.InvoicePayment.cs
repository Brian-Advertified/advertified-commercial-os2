using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Opportunity;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingCommands
{
    private async Task<CommandOutcome> IssueInvoiceOutcomeAsync(
        CommandEnvelope<IssueInvoiceCommand> envelope,
        CancellationToken cancellationToken)
    {
        var order = await store.FindPurchaseOrderAsync(
            envelope.Command.PurchaseOrderId, false, cancellationToken)
            ?? throw new UnauthorizedAccessException("Purchase order access denied.");
        if (order.Status != MasterDataCodes.LifecycleStatuses.Approved)
            throw new InvalidLifecycleTransitionException();
        var source = await SelectedSourceAsync(
            envelope.TenantId, order.ProposalVersionId, order.ProposalOptionId,
            cancellationToken);
        if (order.AmountMinor != source.TotalMinor || order.Currency != source.Currency)
            throw new FundingReviewRequiredException();
        var number = FundingDocumentPolicy.InvoiceNumber(envelope.Command.InvoiceNumber);
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var changed = await store.InsertInvoiceAsync(
            id, envelope, order, source, number, now, cancellationToken);
        if (changed != 1) throw new InvalidLifecycleTransitionException();
        var view = (await store.FindInvoiceAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("The invoice was not persisted.")).ToView();
        return Outcome(
            envelope, view, id, MasterDataReferences.CommercialResourceTypes.Invoice,
            MasterDataReferences.CommercialActions.InvoiceIssued,
            MasterDataReferences.CommercialEventTypes.InvoiceIssued, now);
    }

    private async Task<CommandOutcome> StartPaymentOutcomeAsync(
        CommandEnvelope<StartPaymentCommand> envelope,
        CancellationToken cancellationToken)
    {
        if (envelope.Command.MethodCode != MasterDataCodes.PaymentMethods.ManualEft)
            throw new PaymentMethodUnavailableException();
        var invoice = await store.FindInvoiceAsync(
            envelope.Command.InvoiceId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invoice access denied.");
        if (invoice.Status != MasterDataCodes.LifecycleStatuses.Issued)
            throw new InvalidLifecycleTransitionException();
        var id = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        var changed = await store.InsertPaymentAsync(id, envelope, invoice, now, cancellationToken);
        if (changed != 1) throw new InvalidLifecycleTransitionException();
        var view = (await store.FindPaymentAsync(id, false, cancellationToken)
            ?? throw new InvalidOperationException("The payment was not persisted.")).ToView();
        return Outcome(
            envelope, view, id, MasterDataReferences.CommercialResourceTypes.PaymentIntent,
            MasterDataReferences.CommercialActions.PaymentStarted,
            MasterDataReferences.CommercialEventTypes.PaymentStarted, now);
    }

    private async Task<CommandOutcome> ReconcilePaymentOutcomeAsync(
        Guid id,
        CommandEnvelope<ReconcilePaymentCommand> envelope,
        CancellationToken cancellationToken)
    {
        var payment = await store.FindPaymentAsync(id, true, cancellationToken)
            ?? throw new UnauthorizedAccessException("Payment access denied.");
        if (payment.Status != MasterDataCodes.LifecycleStatuses.Pending)
            throw new InvalidLifecycleTransitionException();
        if (payment.StartedBy == envelope.ActorId.Value)
            throw new UnauthorizedAccessException("A different reviewer must reconcile the payment.");
        var receipt = await ProtectAsync(envelope.Command.Receipt, cancellationToken);
        var reference = FundingDocumentPolicy.Reference(
            envelope.Command.ReconciliationReference);
        var reason = FundingDocumentPolicy.Reason(envelope.Command.Reason);
        var objectKey = ObjectKey(
            envelope.TenantId.Value, "receipts", id, receipt.Sha256);
        var now = timeProvider.GetUtcNow();
        await store.ReconcilePaymentAsync(
            payment, envelope, receipt, objectKey, reference, reason, now, cancellationToken);
        await objectStore.PutAsync(
            objectKey, receipt.Content, receipt.MediaType, cancellationToken);
        var view = (await store.FindPaymentAsync(id, false, cancellationToken))!.ToView();
        return Outcome(
            envelope, view, id, MasterDataReferences.CommercialResourceTypes.PaymentIntent,
            MasterDataReferences.CommercialActions.PaymentConfirmed,
            MasterDataReferences.CommercialEventTypes.PaymentConfirmed, now);
    }

    private static CommandOutcome Outcome<TCommand, TView>(
        CommandEnvelope<TCommand> envelope,
        TView view,
        Guid id,
        ResourceTypeCode resourceType,
        ActionCode action,
        EventTypeCode eventType,
        DateTimeOffset now)
        where TCommand : notnull where TView : notnull =>
        CommandOutcomeFactory.Create(
            envelope, view, id, view switch
            {
                PurchaseOrderView purchaseOrder => purchaseOrder.Version,
                InvoiceView invoice => invoice.Version,
                PaymentIntentView payment => payment.Version,
                _ => throw new InvalidOperationException("Unknown funding view."),
            }, resourceType, action, eventType, now);
}
