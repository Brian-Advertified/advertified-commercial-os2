import { useState } from 'react'
import { inventoryCodes, type InventoryDecision } from '../api/inventory-constants'
import type { InventoryCandidate, InventoryValues } from '../api/inventory-schemas'
import { masterDataDefinitions } from '../generated/master-data-codes'
import { formatMoney, majorAmountToMinor, minorAmountToInput } from '../presentation/format'
import { inventoryAcceptanceCopy } from '../content/inventory-acceptance-copy'

export function InventoryCandidateReview({ candidate, canReview, busy, review }: {
  candidate: InventoryCandidate
  canReview: boolean
  busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
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
    {candidate.values.extension?.acceptanceevaluation && <details><summary>{inventoryAcceptanceCopy.checks}</summary>
      <pre>{candidate.values.extension.acceptanceevaluation}</pre></details>}
    {candidate.validation.length > 0 && <ul className="validation-list">
      {candidate.validation.map((issue) => <li className={issue.isBlocking ? 'blocking' : ''}
        key={`${issue.fieldName}-${issue.code}`}>{issue.message}</li>)}</ul>}
    <details className="evidence-panel"><summary>View {candidate.evidence.length} source-linked fields</summary>
      <div className="evidence-table">{candidate.evidence.map((field) => <div key={field.fieldName}>
        <strong>{field.fieldName.replaceAll('_', ' ')}</strong><span>{field.rawValue ?? 'Not supplied'}</span>
        <small>{field.sourceLocator} · {field.transformation.toLowerCase().replaceAll('_', ' ')}</small>
      </div>)}</div></details>
    {canReview && <CandidateActions candidate={candidate} busy={busy} review={review} />}
  </article>
}

function CandidateActions({ candidate, busy, review }: {
  candidate: InventoryCandidate; busy: boolean
  review: (candidate: InventoryCandidate, decision: InventoryDecision,
    values: InventoryValues | null, reason: string | null) => Promise<void>
}) {
  const [draft, setDraft] = useState<InventoryValues | null>(null)
  const [rateMajor, setRateMajor] = useState('')
  const [error, setError] = useState('')
  const hasBlocking = candidate.validation.some(issue => issue.isBlocking)

  function beginCorrection() {
    const values = structuredClone(candidate.values)
    setDraft(values)
    setRateMajor(values.rateAmountMinor !== null && values.currency
      ? minorAmountToInput(values.rateAmountMinor, values.currency) : '')
    setError('')
  }

  async function saveCorrection() {
    if (!draft) return
    let corrected = draft
    if (rateMajor.trim()) {
      if (!draft.currency) {
        setError('Choose the rate currency before entering an amount.')
        return
      }
      const amount = Number(rateMajor)
      if (!Number.isFinite(amount) || amount < 0) {
        setError('Enter a valid non-negative rate amount.')
        return
      }
      corrected = { ...draft, rateAmountMinor: majorAmountToMinor(amount, draft.currency) }
    } else {
      corrected = { ...draft, rateAmountMinor: null }
    }
    setError('')
    await review(candidate, inventoryCodes.decision.edit, corrected, null)
    setDraft(null)
  }

  return <div className="candidate-actions">
    <div className="candidate-review-primary-actions">
      <button className="primary-button" type="button" disabled={busy || hasBlocking}
        onClick={() => void review(candidate, inventoryCodes.decision.approve, null, null)}>Approve candidate</button>
      <button className="secondary-button" type="button" disabled={busy}
        onClick={beginCorrection}>Correct candidate</button>
      {hasBlocking && <small>Correct the blocking facts or reject this candidate before publication.</small>}
    </div>
    {draft && <CandidateCorrectionEditor draft={draft} setDraft={setDraft}
      rateMajor={rateMajor} setRateMajor={setRateMajor} busy={busy} error={error}
      onSave={() => void saveCorrection()} onCancel={() => { setDraft(null); setError('') }} />}
    <RejectButton candidate={candidate} busy={busy} review={review} />
  </div>
}

function CandidateCorrectionEditor({ draft, setDraft, rateMajor, setRateMajor, busy, error, onSave, onCancel }: {
  draft: InventoryValues
  setDraft: (value: InventoryValues) => void
  rateMajor: string
  setRateMajor: (value: string) => void
  busy: boolean
  error: string
  onSave: () => void
  onCancel: () => void
}) {
  return <section className="candidate-correction-panel">
    <header><div><p className="eyebrow">Human correction</p><h3>Correct candidate facts</h3></div>
      <p>Only change values you can verify against the retained source. The correction is recorded separately from source evidence.</p></header>
    <div className="candidate-correction-grid">
      <TextField label="Product code" value={draft.productCode}
        onChange={value => setDraft({ ...draft, productCode: value })} />
      <TextField label="Product name" value={draft.name}
        onChange={value => setDraft({ ...draft, name: value })} />
      <SelectField label="Channel" value={draft.channel} options={masterDataDefinitions.channels}
        onChange={value => setDraft({ ...draft, channel: value })} />
      <SelectField label="Product type" value={draft.productType} options={masterDataDefinitions.inventoryProductTypes}
        onChange={value => setDraft({ ...draft, productType: value })} />
      <TextField label="Geography" value={draft.geography}
        onChange={value => setDraft({ ...draft, geography: value })} />
      <TextField label="Address" value={draft.address}
        onChange={value => setDraft({ ...draft, address: value })} />
      <NumberField label="Latitude" value={draft.latitude}
        onChange={value => setDraft({ ...draft, latitude: value })} min={-90} max={90} />
      <NumberField label="Longitude" value={draft.longitude}
        onChange={value => setDraft({ ...draft, longitude: value })} min={-180} max={180} />
      <SelectField label="Rate type" value={draft.rateType} options={masterDataDefinitions.rateTypes}
        onChange={value => setDraft({ ...draft, rateType: value })} />
      <SelectField label="Currency" value={draft.currency} options={masterDataDefinitions.currencies}
        onChange={value => setDraft({ ...draft, currency: value })} />
      <label className="field-group">Rate amount
        <input type="number" min="0" step="any" value={rateMajor} disabled={!draft.currency}
          onChange={event => setRateMajor(event.target.value)} />
      </label>
      <SelectField label="Availability" value={draft.availability} options={masterDataDefinitions.availabilityStatuses}
        onChange={value => setDraft({ ...draft, availability: value })} />
    </div>
    <label className="field-group">Description<textarea rows={3} value={draft.description ?? ''}
      onChange={event => setDraft({ ...draft, description: nullableText(event.target.value) })} /></label>
    {error && <p role="alert">{error}</p>}
    <div className="candidate-review-primary-actions">
      <button className="primary-button" type="button" disabled={busy} onClick={onSave}>Save correction & approve</button>
      <button className="secondary-button" type="button" disabled={busy} onClick={onCancel}>Cancel</button>
    </div>
  </section>
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

function TextField({ label, value, onChange }: {
  label: string; value: string | null; onChange: (value: string | null) => void
}) {
  return <label className="field-group">{label}<input value={value ?? ''}
    onChange={event => onChange(nullableText(event.target.value))} /></label>
}

function SelectField({ label, value, options, onChange }: {
  label: string
  value: string | null
  options: readonly { code: string; displayLabel: string; isActive: boolean }[]
  onChange: (value: string | null) => void
}) {
  return <label className="field-group">{label}<select value={value ?? ''}
    onChange={event => onChange(nullableText(event.target.value))}>
    <option value="">Choose…</option>
    {options.filter(option => option.isActive).map(option =>
      <option key={option.code} value={option.code}>{option.displayLabel}</option>)}
  </select></label>
}

function NumberField({ label, value, min, max, onChange }: {
  label: string; value: number | null; min: number; max: number
  onChange: (value: number | null) => void
}) {
  return <label className="field-group">{label}<input type="number" step="any" min={min} max={max}
    value={value ?? ''} onChange={event => onChange(nullableNumber(event.target.value))} /></label>
}

function nullableText(value: string) {
  const result = value.trim()
  return result ? result : null
}

function nullableNumber(value: string) {
  if (!value.trim()) return null
  const result = Number(value)
  return Number.isFinite(result) ? result : null
}

function Fact({ label, value }: { label: string; value: string | null }) {
  return <div><span>{label}</span><strong>{value ?? 'Not supplied'}</strong></div>
}

function money(values: InventoryValues): string | null {
  if (values.rateAmountMinor === null || !values.currency) return null
  return formatMoney(values.rateAmountMinor, values.currency)
}
