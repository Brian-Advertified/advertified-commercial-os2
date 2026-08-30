using Advertified.Commercial.Application.Funding;
using Advertified.Commercial.Application.Foundation;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.Commercial;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingRecordStore
{
    internal Task<int> InsertPurchaseOrderAsync(
        Guid id,
        CommandEnvelope<SubmitPurchaseOrderCommand> envelope,
        FundingSourceRow source,
        string number,
        PreparedFundingDocument document,
        string objectKey,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.purchase_orders (
                id, tenant_id, proposal_version_id, proposal_option_id,
                proposal_decision_id, plan_version_id, po_number, object_key,
                content_sha256, media_type, size_bytes, amount_minor, currency_code,
                status_code, submitted_by, submitted_at_utc, version, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {source.ProposalVersionId},
                {source.ProposalOptionId}, {source.ProposalDecisionId}, {source.PlanVersionId},
                {number}, {objectKey}, {document.Sha256}, {document.MediaType},
                {document.Content.LongLength}, {source.TotalMinor}, {source.Currency},
                {MasterDataCodes.LifecycleStatuses.Submitted}, {envelope.ActorId.Value},
                {now}, 1, {now})
            ON CONFLICT (tenant_id, proposal_decision_id) DO NOTHING
            """, cancellationToken);

    internal async Task ApprovePurchaseOrderAsync(
        PurchaseOrderRow row,
        CommandEnvelope<ApprovePurchaseOrderCommand> envelope,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.purchase_orders
            SET status_code = {MasterDataCodes.LifecycleStatuses.Approved},
                approved_by = {envelope.ActorId.Value}, approved_at_utc = {now},
                reconciliation_reason = {reason}, version = version + 1,
                updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Submitted}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }

    internal Task<int> InsertInvoiceAsync(
        Guid id,
        CommandEnvelope<IssueInvoiceCommand> envelope,
        PurchaseOrderRow order,
        FundingSourceRow source,
        string number,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.invoices (
                id, tenant_id, proposal_version_id, proposal_option_id,
                purchase_order_id, invoice_number, subtotal_minor, fees_minor,
                vat_minor, total_minor, currency_code, status_code,
                issued_by, issued_at_utc, version)
            VALUES ({id}, {envelope.TenantId.Value}, {order.ProposalVersionId},
                {order.ProposalOptionId}, {order.Id}, {number}, {source.SubtotalMinor},
                {source.FeesMinor}, {source.VatMinor}, {source.TotalMinor}, {source.Currency},
                {MasterDataCodes.LifecycleStatuses.Issued}, {envelope.ActorId.Value}, {now}, 1)
            ON CONFLICT (tenant_id, purchase_order_id) DO NOTHING
            """, cancellationToken);

    internal Task<int> InsertPaymentAsync(
        Guid id,
        CommandEnvelope<StartPaymentCommand> envelope,
        InvoiceRow invoice,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO commercial.payment_intents (
                id, tenant_id, proposal_version_id, proposal_option_id,
                purchase_order_id, invoice_id, method_code, amount_minor,
                currency_code, status_code, started_by, started_at_utc, version, updated_at_utc)
            VALUES ({id}, {envelope.TenantId.Value}, {invoice.ProposalVersionId},
                {invoice.ProposalOptionId}, {invoice.PurchaseOrderId}, {invoice.Id},
                {MasterDataCodes.PaymentMethods.ManualEft}, {invoice.TotalMinor},
                {invoice.Currency}, {MasterDataCodes.LifecycleStatuses.Pending},
                {envelope.ActorId.Value}, {now}, 1, {now})
            ON CONFLICT (tenant_id, invoice_id) DO NOTHING
            """, cancellationToken);

    internal async Task ReconcilePaymentAsync(
        PaymentIntentRow row,
        CommandEnvelope<ReconcilePaymentCommand> envelope,
        PreparedFundingDocument receipt,
        string objectKey,
        string reference,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var changed = await DbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE commercial.payment_intents
            SET status_code = {MasterDataCodes.LifecycleStatuses.Confirmed},
                reconciled_by = {envelope.ActorId.Value}, reconciled_at_utc = {now},
                reconciliation_reference = {reference}, reconciliation_reason = {reason},
                receipt_object_key = {objectKey}, receipt_sha256 = {receipt.Sha256},
                version = version + 1, updated_at_utc = {now}
            WHERE id = {row.Id} AND tenant_id = {envelope.TenantId.Value}
              AND status_code = {MasterDataCodes.LifecycleStatuses.Pending}
              AND version = {envelope.ExpectedVersion}
            """, cancellationToken);
        if (changed != 1) throw new VersionConflictException();
    }
}
