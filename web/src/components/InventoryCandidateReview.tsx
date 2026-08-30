import { useState, type FormEvent } from 'react'
import { inventoryCodes, type InventoryDecision } from '../api/inventory-constants'
import type { InventoryCandidate, InventoryValues } from '../api/inventory-schemas'
import { formatMoney } from '../presentation/format'

export function InventoryCandidateReview({ candidate, canReview, busy, review }: {
  candidate: InventoryCandidate
  canReview: boolean
  busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
  const [editing, setEditing] = useState(false)
  function correct(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    void review(candidate, inventoryCodes.decision.edit,
      valuesFrom(new FormData(event.currentTarget), candidate.values), null)
  }
  return <article className="detail-card candidate-card">
    <header className="page-heading-split"><div><p className="eyebrow">Source row {candidate.rowNumber}</p>
      <h2>{candidate.values.name ?? 'Product identity missing'}</h2>
      <p>{candidate.sourceLocator}</p></div><span className="status-chip">{candidate.status}</span></header>
    <div className="candidate-facts">
      <Fact label="Code" value={candidate.values.productCode} /><Fact label="Channel" value={candidate.values.channel} />
      <Fact label="Geography" value={candidate.values.geography} />
      <Fact label="Rate" value={money(candidate.values)} />
      <Fact label="Availability" value={candidate.values.availability} />
    </div>
    {candidate.validation.length > 0 && <ul className="validation-list">
      {candidate.validation.map((issue) => <li className={issue.isBlocking ? 'blocking' : ''}
        key={`${issue.fieldName}-${issue.code}`}>{issue.message}</li>)}</ul>}
    <details className="evidence-panel"><summary>View {candidate.evidence.length} source-linked fields</summary>
      <div className="evidence-table">{candidate.evidence.map((field) => <div key={field.fieldName}>
        <strong>{field.fieldName.replaceAll('_', ' ')}</strong><span>{field.rawValue ?? 'Not supplied'}</span>
        <small>{field.sourceLocator} · {field.transformation.toLowerCase().replaceAll('_', ' ')}</small>
      </div>)}</div></details>
    {canReview && !editing && <div className="candidate-actions">
      <button className="primary-button" disabled={busy || candidate.validation.some((item) => item.isBlocking)}
        onClick={() => void review(candidate, inventoryCodes.decision.approve, null, null)}>Approve source values</button>
      <button className="secondary-button" disabled={busy} onClick={() => setEditing(true)}>Correct fields</button>
      <RejectButton candidate={candidate} busy={busy} review={review} />
    </div>}
    {canReview && editing && <CorrectionForm candidate={candidate} busy={busy} submit={correct}
      cancel={() => setEditing(false)} />}
  </article>
}

function CorrectionForm({ candidate, busy, submit, cancel }: {
  candidate: InventoryCandidate; busy: boolean
  submit: (event: FormEvent<HTMLFormElement>) => void; cancel: () => void
}) {
  const value = candidate.values
  return <form className="correction-form" onSubmit={submit}>
    <Input name="productCode" label="Product code" value={value.productCode} />
    <Input name="name" label="Product name" value={value.name} />
    <Input name="channel" label="Channel code" value={value.channel} />
    <Input name="productType" label="Product type" value={value.productType} />
    <Input name="geography" label="Geography" value={value.geography} />
    <Input name="address" label="Address" value={value.address} required={false} />
    <Input name="latitude" label="Latitude" value={value.latitude} type="number" />
    <Input name="longitude" label="Longitude" value={value.longitude} type="number" />
    <Input name="rateType" label="Rate type" value={value.rateType} />
    <Input name="currency" label="Currency" value={value.currency} />
    <Input name="rateAmountMinor" label="Rate in minor units" value={value.rateAmountMinor} type="number" />
    <Input name="availability" label="Availability" value={value.availability} />
    <div className="candidate-actions"><button className="primary-button" disabled={busy}>Save correction and approve</button>
      <button className="secondary-button" type="button" onClick={cancel}>Cancel</button></div>
  </form>
}

function RejectButton({ candidate, busy, review }: {
  candidate: InventoryCandidate; busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
  const [reason, setReason] = useState<string>(inventoryCodes.rejectionReason.missingInformation)
  return <div className="reject-action"><label>Rejection reason<select value={reason}
    onChange={(event) => setReason(event.target.value)}>
    <option value={inventoryCodes.rejectionReason.missingInformation}>Missing information</option>
    <option value={inventoryCodes.rejectionReason.duplicate}>Duplicate</option>
    <option value={inventoryCodes.rejectionReason.qualityIssue}>Quality issue</option>
    <option value={inventoryCodes.rejectionReason.staleRate}>Stale rate</option></select></label>
    <button className="text-action" type="button" disabled={busy}
      onClick={() => void review(candidate, inventoryCodes.decision.reject, null, reason)}>Reject candidate</button></div>
}

function Input({ name, label, value, type = 'text', required = true }: {
  name: string; label: string; value: string | number | null; type?: string; required?: boolean
}) { return <label className="field-group">{label}<input name={name} type={type}
  step={type === 'number' ? 'any' : undefined} defaultValue={value ?? ''} required={required} /></label> }

function Fact({ label, value }: { label: string; value: string | null }) {
  return <div><span>{label}</span><strong>{value ?? 'Not supplied'}</strong></div>
}

function money(values: InventoryValues): string | null {
  if (values.rateAmountMinor === null || !values.currency) return null
  return formatMoney(values.rateAmountMinor, values.currency, 2)
}

function valuesFrom(form: FormData, original: InventoryValues): InventoryValues {
  const text = (name: string) => String(form.get(name) ?? '').trim() || null
  const number = (name: string) => {
    const value = text(name); return value === null ? null : Number(value)
  }
  return { productCode: text('productCode'), name: text('name'), channel: text('channel'),
    productType: text('productType'), geography: text('geography'), address: text('address'),
    latitude: number('latitude'), longitude: number('longitude'), rateType: text('rateType'),
    currency: text('currency'), rateAmountMinor: number('rateAmountMinor'),
    availability: text('availability'), extension: original.extension }
}
