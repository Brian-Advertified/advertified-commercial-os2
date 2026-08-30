using Advertified.Commercial.Application.Commands;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Inventory;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Advertified.Commercial.Infrastructure.Foundation;
using Advertified.Commercial.Infrastructure.Campaign;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingCommands(
    FundingRecordStore store,
    IInventoryObjectStore objectStore,
    IInventoryMalwareScanner malwareScanner,
    CampaignRecordStore campaignStore,
    CommandDispatcher dispatcher,
    TimeProvider timeProvider) : IFundingCommands
{
    public Task<CommandResult<PurchaseOrderView>> SubmitPurchaseOrderAsync(
        CommandEnvelope<SubmitPurchaseOrderCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<SubmitPurchaseOrderCommand, PurchaseOrderView>(
            envelope, MasterDataReferences.Permissions.PurchaseOrderSubmit,
            token => SubmitPurchaseOrderOutcomeAsync(envelope, token), cancellationToken);

    public Task<CommandResult<PurchaseOrderView>> ApprovePurchaseOrderAsync(
        Guid purchaseOrderId,
        CommandEnvelope<ApprovePurchaseOrderCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<ApprovePurchaseOrderCommand, PurchaseOrderView>(
            envelope, MasterDataReferences.Permissions.PurchaseOrderApprove,
            token => ApprovePurchaseOrderOutcomeAsync(purchaseOrderId, envelope, token),
            cancellationToken);

    public Task<CommandResult<InvoiceView>> IssueInvoiceAsync(
        CommandEnvelope<IssueInvoiceCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<IssueInvoiceCommand, InvoiceView>(
            envelope, MasterDataReferences.Permissions.InvoiceIssue,
            token => IssueInvoiceOutcomeAsync(envelope, token), cancellationToken);

    public Task<CommandResult<PaymentIntentView>> StartPaymentAsync(
        CommandEnvelope<StartPaymentCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<StartPaymentCommand, PaymentIntentView>(
            envelope, MasterDataReferences.Permissions.PaymentCreate,
            token => StartPaymentOutcomeAsync(envelope, token), cancellationToken);

    public Task<CommandResult<PaymentIntentView>> ReconcilePaymentAsync(
        Guid paymentIntentId,
        CommandEnvelope<ReconcilePaymentCommand> envelope,
        CancellationToken cancellationToken) => DispatchAsync<ReconcilePaymentCommand, PaymentIntentView>(
            envelope, MasterDataReferences.Permissions.PaymentReconcile,
            token => ReconcilePaymentOutcomeAsync(paymentIntentId, envelope, token),
            cancellationToken);

    private async Task<CommandResult<TResult>> DispatchAsync<TCommand, TResult>(
        CommandEnvelope<TCommand> envelope,
        PermissionCode permission,
        Func<CancellationToken, Task<CommandOutcome>> execute,
        CancellationToken cancellationToken)
        where TCommand : notnull where TResult : notnull
    {
        var receipt = await dispatcher.DispatchAsync(
            envelope, permission, execute, cancellationToken);
        return CommandOutcomeFactory.ToResult<TResult>(receipt);
    }
}
