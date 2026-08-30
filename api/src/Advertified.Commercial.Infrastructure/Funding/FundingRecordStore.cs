using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Infrastructure.MasterData;
using Advertified.Commercial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingRecordStore(GovernanceDbContext dbContext)
{
    internal GovernanceDbContext DbContext => dbContext;

    internal async Task<IDbContextTransaction> BeginSessionAsync(
        ActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ApplicationDatabaseSession.SetAsync(
            dbContext, new UserId(actorId.Value), tenantId, cancellationToken);
        return transaction;
    }

    internal const string PurchaseOrderSelect = """
        SELECT po.id AS "Id", po.proposal_version_id AS "ProposalVersionId",
            po.proposal_option_id AS "ProposalOptionId",
            po.proposal_decision_id AS "ProposalDecisionId",
            po.plan_version_id AS "PlanVersionId",
            po.po_number AS "PurchaseOrderNumber", po.object_key AS "ObjectKey",
            po.content_sha256 AS "ContentSha256", po.media_type AS "MediaType",
            po.size_bytes AS "SizeBytes", po.amount_minor AS "AmountMinor",
            po.currency_code AS "Currency", po.status_code AS "Status",
            po.submitted_by AS "SubmittedBy", po.submitted_at_utc AS "SubmittedAtUtc",
            po.approved_by AS "ApprovedBy", po.approved_at_utc AS "ApprovedAtUtc",
            po.reconciliation_reason AS "ReconciliationReason", po.version AS "Version"
        FROM commercial.purchase_orders po
        """;

    internal const string InvoiceSelect = """
        SELECT invoice.id AS "Id", invoice.proposal_version_id AS "ProposalVersionId",
            invoice.proposal_option_id AS "ProposalOptionId",
            invoice.purchase_order_id AS "PurchaseOrderId",
            invoice.invoice_number AS "InvoiceNumber",
            invoice.subtotal_minor AS "SubtotalMinor", invoice.fees_minor AS "FeesMinor",
            invoice.vat_minor AS "VatMinor", invoice.total_minor AS "TotalMinor",
            invoice.currency_code AS "Currency", invoice.status_code AS "Status",
            invoice.issued_by AS "IssuedBy", invoice.issued_at_utc AS "IssuedAtUtc",
            invoice.version AS "Version"
        FROM commercial.invoices invoice
        """;

    internal const string PaymentSelect = """
        SELECT payment.id AS "Id", payment.proposal_version_id AS "ProposalVersionId",
            payment.proposal_option_id AS "ProposalOptionId",
            payment.purchase_order_id AS "PurchaseOrderId", payment.invoice_id AS "InvoiceId",
            payment.method_code AS "MethodCode", payment.amount_minor AS "AmountMinor",
            payment.currency_code AS "Currency", payment.status_code AS "Status",
            payment.started_by AS "StartedBy", payment.started_at_utc AS "StartedAtUtc",
            payment.reconciled_by AS "ReconciledBy",
            payment.reconciled_at_utc AS "ReconciledAtUtc",
            payment.reconciliation_reference AS "ReconciliationReference",
            payment.reconciliation_reason AS "ReconciliationReason",
            payment.receipt_object_key AS "ReceiptObjectKey",
            payment.receipt_sha256 AS "ReceiptSha256", payment.version AS "Version"
        FROM commercial.payment_intents payment
        """;
}
