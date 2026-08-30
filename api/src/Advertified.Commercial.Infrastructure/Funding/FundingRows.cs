using Advertified.Commercial.Application.Funding;

namespace Advertified.Commercial.Infrastructure.Funding;

internal sealed record FundingSourceRow(
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    Guid PlanVersionId,
    long SubtotalMinor,
    long FeesMinor,
    long VatMinor,
    long TotalMinor,
    string Currency);

internal sealed record PurchaseOrderRow(
    Guid Id,
    Guid ProposalVersionId,
    Guid ProposalOptionId,
    Guid ProposalDecisionId,
    Guid PlanVersionId,
    string PurchaseOrderNumber,
    string ObjectKey,
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
    long Version)
{
    internal PurchaseOrderView ToView() => new(
        Id, ProposalVersionId, ProposalOptionId, ProposalDecisionId,
        PurchaseOrderNumber, ContentSha256, MediaType, SizeBytes,
        AmountMinor, Currency, Status, SubmittedBy, SubmittedAtUtc,
        ApprovedBy, ApprovedAtUtc, ReconciliationReason, Version);
}

internal sealed record InvoiceRow(
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
    long Version)
{
    internal InvoiceView ToView() => new(
        Id, ProposalVersionId, ProposalOptionId, PurchaseOrderId, InvoiceNumber,
        SubtotalMinor, FeesMinor, VatMinor, TotalMinor, Currency, Status,
        IssuedBy, IssuedAtUtc, Version);
}

internal sealed record PaymentIntentRow(
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
    string? ReceiptObjectKey,
    string? ReceiptSha256,
    long Version)
{
    internal PaymentIntentView ToView() => new(
        Id, ProposalVersionId, ProposalOptionId, PurchaseOrderId, InvoiceId,
        MethodCode, AmountMinor, Currency, Status, StartedBy, StartedAtUtc,
        ReconciledBy, ReconciledAtUtc, ReconciliationReference,
        ReconciliationReason, ReceiptSha256, Version);
}
