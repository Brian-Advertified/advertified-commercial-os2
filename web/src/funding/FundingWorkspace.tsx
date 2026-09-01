import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { fundingApi } from '../api/funding-client'
import {
  purchaseOrderInputSchema,
  type FundingWorkspace as FundingData,
} from '../api/funding-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, majorAmountToMinor, minorAmountToInput } from '../presentation/format'
import { FundingRecordCard } from './FundingRecordCard'

export type FundingSelection = {
  proposalVersionId: string
  proposalOptionId: string
  amountMinor: number
  currency: string
}

export type FundingRun = (
  action: () => Promise<unknown>,
  successMessage: string,
) => Promise<void>

type Props = {
  tenantId: string
  token: string
  workspace: FundingData
  selection: FundingSelection | null
  busy: boolean
  canSubmit: boolean
  canAdminister: boolean
  canStartPayment: boolean
  run: FundingRun
}

export function FundingWorkspace(props: Props) {
  const existingOrder = props.selection && props.workspace.purchaseOrders.some(order =>
    order.proposalVersionId === props.selection?.proposalVersionId &&
    order.proposalOptionId === props.selection.proposalOptionId)
  return <section className="funding-page" aria-labelledby="funding-title">
    <FundingHero workspace={props.workspace} />
    {props.selection && <SelectedOptionSummary selection={props.selection} />}
    {props.canSubmit && props.selection && !existingOrder &&
      <PurchaseOrderForm {...props} selection={props.selection} />}
    {!props.selection && props.canSubmit && <OpenProposalPrompt />}
    <FundingRecords {...props} />
  </section>
}

function FundingHero({ workspace }: { workspace: FundingData }) {
  const confirmed = workspace.payments.filter(payment =>
    payment.status === masterDataCodes.lifecycleStatuses.confirmed).length
  return <header className="funding-hero"><div><p className="eyebrow eyebrow-light">Funding and purchase order</p>
    <h1 id="funding-title">Turn an accepted proposal into accountable funding.</h1>
    <p>Every purchase order, invoice and payment decision remains tied to the exact option selected by the client.</p></div>
    <dl><div><dt>Purchase orders</dt><dd>{workspace.purchaseOrders.length}</dd></div>
      <div><dt>Invoices</dt><dd>{workspace.invoices.length}</dd></div>
      <div><dt>Confirmed payments</dt><dd>{confirmed}</dd></div></dl></header>
}

function SelectedOptionSummary({ selection }: { selection: FundingSelection }) {
  return <article className="funding-option-summary"><span><Icon name="proposal" /></span>
    <div><p className="eyebrow">Client-selected option</p><h2>Funding must match the accepted investment</h2>
      <p>{formatMoney(selection.amountMinor, selection.currency)} · {selection.currency}</p></div>
    <span className="status-chip status-positive">Selected</span></article>
}

function OpenProposalPrompt() {
  return <article className="funding-open-proposal"><span><Icon name="proposal" /></span><div>
    <h2>Open the client-selected proposal first</h2>
    <p>Funding starts from an exact accepted option so that users never need to type internal record identifiers.</p>
    <Link className="secondary-button" to="/home">Return to current work</Link></div></article>
}

function PurchaseOrderForm(props: Props & { selection: FundingSelection }) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const values = new FormData(form)
    const document = values.get('document')
    const amount = Number(values.get('amount'))
    const candidate = purchaseOrderInputSchema.safeParse({
      proposalVersionId: props.selection.proposalVersionId,
      proposalOptionId: props.selection.proposalOptionId,
      purchaseOrderNumber: String(values.get('purchaseOrderNumber') ?? ''),
      amountMinor: majorAmountToMinor(amount, props.selection.currency),
      currency: props.selection.currency,
    })
    if (!candidate.success || !(document instanceof File) || document.size === 0) {
      setError('Enter the purchase order number, matching amount and signed document.')
      return
    }
    setError(null)
    void props.run(
      () => fundingApi.submitPurchaseOrder(
        props.tenantId, candidate.data, document, props.token),
      'The purchase order was submitted for independent reconciliation.',
    )
  }
  return <form className="funding-action-card" onSubmit={submit}>
    <header><div><p className="eyebrow">Submit funding evidence</p><h2>Signed purchase order</h2>
      <p>The amount and currency must match the client-selected option exactly.</p></div>
      <span className="status-chip status-warning">Review required</span></header>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div className="funding-form-grid">
      <label className="field-group">Purchase order number
        <input name="purchaseOrderNumber" required maxLength={200} /></label>
      <label className="field-group">Purchase order amount ({props.selection.currency})
        <input name="amount" type="number" required min="0" step="any"
          defaultValue={minorAmountToInput(
            props.selection.amountMinor, props.selection.currency)} /></label>
      <label className="field-group funding-file-field">Signed purchase order
        <input name="document" type="file" required accept="application/pdf,image/png,image/jpeg" /></label>
    </div>
    <footer><span><Icon name="shield" /> Submission does not approve payment or create a campaign.</span>
      <button className="primary-button" disabled={props.busy}>Submit for review</button></footer>
  </form>
}

function FundingRecords(props: Props) {
  const orders = [...props.workspace.purchaseOrders]
    .sort((left, right) => right.submittedAtUtc.localeCompare(left.submittedAtUtc))
  return <section className="funding-records" aria-labelledby="funding-records-title">
    <header><div><p className="eyebrow">Funding history</p><h2 id="funding-records-title">Purchase orders, invoices and payments</h2></div>
      <span className="status-chip status-neutral">{orders.length} records</span></header>
    {orders.length === 0 ? <div className="funding-empty"><Icon name="money" /><div>
      <h3>No funding record has been submitted</h3><p>An accepted proposal option is required first.</p></div></div>
      : <div className="funding-record-grid">{orders.map(order =>
        <FundingRecordCard key={order.id} {...props} order={order}
          invoice={props.workspace.invoices.find(item => item.purchaseOrderId === order.id) ?? null}
          payment={props.workspace.payments.find(item => item.purchaseOrderId === order.id) ?? null} />)}</div>}
  </section>
}
