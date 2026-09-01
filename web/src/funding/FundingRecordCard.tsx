import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { fundingApi } from '../api/funding-client'
import {
  invoiceInputSchema,
  paymentReconciliationSchema,
  purchaseOrderApprovalSchema,
  type Invoice,
  type PaymentIntent,
  type PurchaseOrder,
} from '../api/funding-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'
import type { FundingRun } from './FundingWorkspace'

type Props = {
  tenantId: string
  token: string
  order: PurchaseOrder
  invoice: Invoice | null
  payment: PaymentIntent | null
  busy: boolean
  canAdminister: boolean
  canStartPayment: boolean
  run: FundingRun
}

export function FundingRecordCard(props: Props) {
  return <article className="funding-record-card">
    <FundingRecordHeader order={props.order} />
    <FundingTimeline {...props} />
    <FundingActions {...props} />
  </article>
}

function FundingRecordHeader({ order }: { order: PurchaseOrder }) {
  return <header><div><span>Purchase order</span><h3>{order.purchaseOrderNumber}</h3>
    <p>{formatMoney(order.amountMinor, order.currency)} · submitted {formatDateTime(order.submittedAtUtc)}</p></div>
    <span className={`status-chip ${statusTone(order.status)}`}>{humanizeCode(order.status, true)}</span></header>
}

function FundingTimeline({ order, invoice, payment }: Props) {
  const steps = [
    { label: 'PO submitted', complete: true, detail: formatDateTime(order.submittedAtUtc) },
    { label: 'PO reconciled', complete: Boolean(order.approvedAtUtc),
      detail: order.approvedAtUtc ? formatDateTime(order.approvedAtUtc) : 'Awaiting independent review' },
    { label: 'Invoice issued', complete: Boolean(invoice),
      detail: invoice ? invoice.invoiceNumber : 'Not issued' },
    { label: 'Payment confirmed', complete: payment?.status === masterDataCodes.lifecycleStatuses.confirmed,
      detail: payment ? humanizeCode(payment.status, true) : 'Not started' },
  ]
  return <ol className="funding-timeline">{steps.map((step, index) =>
    <li className={step.complete ? 'is-complete' : ''} key={step.label}>
      <span>{step.complete ? '✓' : index + 1}</span><div><strong>{step.label}</strong><small>{step.detail}</small></div>
    </li>)}</ol>
}

function FundingActions(props: Props) {
  return <div className="funding-record-actions">{fundingAction(props)}</div>
}

function fundingAction(props: Props) {
  const actions = [
    { visible: paymentConfirmed(props), content: <FundingComplete /> },
    { visible: paymentNeedsReview(props), content: props.payment &&
      <PaymentReconciliationForm {...props} payment={props.payment} /> },
    { visible: paymentCanStart(props), content: <StartPaymentAction {...props} /> },
    { visible: invoiceCanIssue(props), content: <InvoiceForm {...props} /> },
    { visible: orderCanApprove(props), content: <PurchaseOrderApprovalForm {...props} /> },
  ]
  return actions.find(action => action.visible)?.content ?? null
}

function paymentConfirmed(props: Props) {
  return props.payment?.status === masterDataCodes.lifecycleStatuses.confirmed
}

function paymentNeedsReview(props: Props) {
  return props.payment?.status === masterDataCodes.lifecycleStatuses.pending &&
    props.canAdminister
}

function paymentCanStart(props: Props) {
  return Boolean(props.invoice) && !props.payment && props.canStartPayment
}

function invoiceCanIssue(props: Props) {
  return props.order.status === masterDataCodes.lifecycleStatuses.approved &&
    !props.invoice && props.canAdminister
}

function orderCanApprove(props: Props) {
  return props.order.status === masterDataCodes.lifecycleStatuses.submitted &&
    props.canAdminister
}

function FundingComplete() {
  return <div className="funding-complete"><Icon name="shield" /><span>
    <strong>Funding confirmed</strong><small>A planned campaign is created from this exact selected option.</small></span>
    <Link className="primary-button" to="/campaigns">Open campaigns</Link></div>
}

function PurchaseOrderApprovalForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const parsed = purchaseOrderApprovalSchema.safeParse({
      reconciliationReason: values.get('reconciliationReason'),
    })
    if (!parsed.success) {
      setError('Explain how the purchase order was reconciled to the accepted option.')
      return
    }
    setError(null)
    void props.run(
      () => fundingApi.approvePurchaseOrder(
        props.tenantId, props.order, parsed.data.reconciliationReason, props.token),
      'The purchase order was independently reconciled and approved.',
    )
  }
  return <form className="funding-inline-form" onSubmit={submit}>
    <h4>Reconcile purchase order</h4>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <label className="field-group">Reconciliation reason
      <textarea name="reconciliationReason" required maxLength={1000} rows={3} /></label>
    <button className="primary-button" disabled={props.busy}>Approve reconciled PO</button>
  </form>
}

function InvoiceForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = invoiceInputSchema.safeParse({
      invoiceNumber: new FormData(event.currentTarget).get('invoiceNumber'),
    })
    if (!parsed.success) {
      setError('Enter the approved invoice number.')
      return
    }
    setError(null)
    void props.run(
      () => fundingApi.issueInvoice(
        props.tenantId, props.order, parsed.data.invoiceNumber, props.token),
      'The reconciled invoice was issued from the approved purchase order.',
    )
  }
  return <form className="funding-inline-form funding-inline-row" onSubmit={submit}>
    <label className="field-group">Invoice number
      <input name="invoiceNumber" required maxLength={200} /></label>
    <button className="primary-button" disabled={props.busy}>Issue invoice</button>
    {error && <p className="inline-alert" role="alert">{error}</p>}
  </form>
}

function StartPaymentAction(props: Props) {
  return <div className="funding-inline-action"><div><h4>Record expected payment</h4>
    <p>The current launch method is manual EFT. Confirmation still requires separate receipt review.</p></div>
    <button className="primary-button" disabled={props.busy} onClick={() => void props.run(
      () => fundingApi.startPayment(
        props.tenantId, props.invoice!, masterDataCodes.paymentMethods.manualEft, props.token),
      'The manual EFT payment record was opened for reconciliation.',
    )}>Start payment record</button></div>
}

function PaymentReconciliationForm(props: Props & { payment: PaymentIntent }) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const receipt = values.get('receipt')
    const parsed = paymentReconciliationSchema.safeParse({
      reconciliationReference: values.get('reconciliationReference'),
      reason: values.get('reason'),
    })
    if (!parsed.success || !(receipt instanceof File) || receipt.size === 0) {
      setError('Provide the bank reference, reconciliation reason and receipt evidence.')
      return
    }
    setError(null)
    void props.run(
      () => fundingApi.reconcilePayment(
        props.tenantId, props.payment, parsed.data.reconciliationReference,
        parsed.data.reason, receipt, props.token),
      'Payment evidence was reconciled and the funded campaign was created.',
    )
  }
  return <form className="funding-inline-form" onSubmit={submit}>
    <h4>Reconcile payment evidence</h4>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className="funding-form-grid"><label className="field-group">Bank reference
      <input name="reconciliationReference" required maxLength={300} /></label>
      <label className="field-group">Receipt evidence
        <input name="receipt" type="file" required accept="application/pdf,image/png,image/jpeg" /></label></div>
    <label className="field-group">Reconciliation reason
      <textarea name="reason" required maxLength={1000} rows={3} /></label>
    <button className="primary-button" disabled={props.busy}>Confirm reconciled payment</button>
  </form>
}

function statusTone(status: string) {
  if (status === masterDataCodes.lifecycleStatuses.approved ||
      status === masterDataCodes.lifecycleStatuses.confirmed) return 'status-positive'
  if (status === masterDataCodes.lifecycleStatuses.rejected) return 'status-danger'
  return 'status-warning'
}
