using Advertified.Commercial.Application.Funding;
using System.Runtime.CompilerServices;
using Advertified.Commercial.Domain.Governance;
using Advertified.Commercial.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace Advertified.Commercial.Infrastructure.Funding;

public sealed partial class FundingRecordStore
{
    internal Task<FundingSourceRow?> FindSelectedSourceAsync(
        TenantId tenantId,
        Guid proposalVersionId,
        Guid proposalOptionId,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<FundingSourceRow>($"""
            SELECT proposal.id AS "ProposalVersionId", option.id AS "ProposalOptionId",
                decision.id AS "ProposalDecisionId", plan.id AS "PlanVersionId",
                plan.subtotal_minor AS "SubtotalMinor", plan.fees_minor AS "FeesMinor",
                plan.vat_minor AS "VatMinor", plan.total_minor AS "TotalMinor",
                plan.currency_code AS "Currency"
            FROM commercial.proposal_versions proposal
            JOIN commercial.proposal_decisions decision
              ON decision.tenant_id = proposal.tenant_id
             AND decision.proposal_version_id = proposal.id
            JOIN commercial.proposal_options option
              ON option.tenant_id = decision.tenant_id AND option.id = decision.option_id
            JOIN commercial.media_plan_versions plan
              ON plan.tenant_id = option.tenant_id AND plan.id = option.plan_version_id
            WHERE proposal.tenant_id = {tenantId.Value}
              AND proposal.id = {proposalVersionId} AND option.id = {proposalOptionId}
              AND proposal.status_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND decision.decision_code = {MasterDataCodes.LifecycleStatuses.Selected}
              AND plan.status_code = {MasterDataCodes.LifecycleStatuses.Approved}
              AND option.budget_minor = plan.total_minor
              AND option.currency_code = plan.currency_code
            """).SingleOrDefaultAsync(cancellationToken);

    internal Task<PurchaseOrderRow?> FindPurchaseOrderAsync(
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PurchaseOrderRow>(FormattableStringFactory.Create(
            PurchaseOrderSelect + " WHERE po.id = {0}" + (forUpdate ? " FOR UPDATE OF po" : string.Empty), id))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<InvoiceRow?> FindInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<InvoiceRow>(FormattableStringFactory.Create(
            InvoiceSelect + " WHERE invoice.id = {0}", id))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<PaymentIntentRow?> FindPaymentAsync(
        Guid id,
        bool forUpdate,
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PaymentIntentRow>(FormattableStringFactory.Create(
            PaymentSelect + " WHERE payment.id = {0}" +
            (forUpdate ? " FOR UPDATE OF payment" : string.Empty), id))
            .SingleOrDefaultAsync(cancellationToken);

    internal Task<List<PurchaseOrderRow>> ListPurchaseOrdersAsync(
        CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PurchaseOrderRow>(FormattableStringFactory.Create(
            PurchaseOrderSelect + " ORDER BY po.submitted_at_utc DESC, po.id DESC"))
            .ToListAsync(cancellationToken);

    internal Task<List<InvoiceRow>> ListInvoicesAsync(CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<InvoiceRow>(FormattableStringFactory.Create(
            InvoiceSelect + " ORDER BY invoice.issued_at_utc DESC, invoice.id DESC"))
            .ToListAsync(cancellationToken);

    internal Task<List<PaymentIntentRow>> ListPaymentsAsync(CancellationToken cancellationToken) =>
        DbContext.Database.SqlQuery<PaymentIntentRow>(FormattableStringFactory.Create(
            PaymentSelect + " ORDER BY payment.started_at_utc DESC, payment.id DESC"))
            .ToListAsync(cancellationToken);
}
