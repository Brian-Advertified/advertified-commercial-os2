using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;

namespace Advertified.Commercial.Application.Funding;

public sealed record FundingDocument(string FileName, string MediaType, byte[] Content);

public sealed record SubmitPurchaseOrderCommand(
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    string PurchaseOrderNumber,
    long AmountMinor,
    string Currency,
    FundingDocument Document);

public sealed record ApprovePurchaseOrderCommand(string ReconciliationReason);
public sealed record IssueInvoiceCommand(Guid PurchaseOrderId, string InvoiceNumber);
public sealed record StartPaymentCommand(Guid InvoiceId, string MethodCode);

public sealed record ReconcilePaymentCommand(
    string ReconciliationReference,
    string Reason,
    FundingDocument Receipt);

public sealed record PurchaseOrderView(
    Guid Id,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    string PurchaseOrderNumber,
    string ContentSha256,
    string MediaType,
    long SizeBytes,
    long AmountMinor,
    string Currency,
    string Status,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAtUtc,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAtUtc,
    string? ReconciliationReason,
    long Version);

public sealed record InvoiceView(
    Guid Id,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid PurchaseOrderId,
    string InvoiceNumber,
    long SubtotalMinor,
    long FeesMinor,
    long VatMinor,
    long TotalMinor,
    string Currency,
    string Status,
    Guid IssuedBy,
    DateTimeOffset IssuedAtUtc,
    long Version);

public sealed record PaymentIntentView(
    Guid Id,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid PurchaseOrderId,
    Guid InvoiceId,
    string MethodCode,
    long AmountMinor,
    string Currency,
    string Status,
    Guid StartedBy,
    DateTimeOffset StartedAtUtc,
    Guid? ReconciledBy,
    DateTimeOffset? ReconciledAtUtc,
    string? ReconciliationReference,
    string? ReconciliationReason,
    string? ReceiptSha256,
    long Version);

public sealed record FundingWorkspaceView(
    IReadOnlyList<PurchaseOrderView> PurchaseOrders,
    IReadOnlyList<InvoiceView> Invoices,
    IReadOnlyList<PaymentIntentView> Payments);

public interface IFundingCommands
{
    Task<CommandResult<PurchaseOrderView>> SubmitPurchaseOrderAsync(
        CommandEnvelope<SubmitPurchaseOrderCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PurchaseOrderView>> ApprovePurchaseOrderAsync(
        Guid purchaseOrderId,
        CommandEnvelope<ApprovePurchaseOrderCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<InvoiceView>> IssueInvoiceAsync(
        CommandEnvelope<IssueInvoiceCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PaymentIntentView>> StartPaymentAsync(
        CommandEnvelope<StartPaymentCommand> envelope,
        CancellationToken cancellationToken);

    Task<CommandResult<PaymentIntentView>> ReconcilePaymentAsync(
        Guid paymentIntentId,
        CommandEnvelope<ReconcilePaymentCommand> envelope,
        CancellationToken cancellationToken);
}

public interface IFundingReader
{
    Task<FundingWorkspaceView> GetWorkspaceAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);
}

public sealed class FundingReviewRequiredException : Exception;
public sealed class PaymentMethodUnavailableException : Exception;
