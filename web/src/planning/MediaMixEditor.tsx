import { useMemo, useState } from 'react'
import { z } from 'zod'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import type { MediaAllocation, MediaMix, RunningPeriod } from '../api/planning-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'

const periodSchema = z.object({ start: z.iso.date(), end: z.iso.date() })
  .refine((value) => value.end >= value.start, { message: 'End date must be after start date.' })

export function MediaMixEditor({ mix, busy, onSave, onApprove, onRevise }: {
  mix: MediaMix
  busy: boolean
  onSave: (allocations: MediaAllocation[]) => Promise<void>
  onApprove: () => Promise<void>
  onRevise: () => Promise<void>
}) {
  const [allocations, setAllocations] = useState<MediaAllocation[]>(mix.allocations)
  const [error, setError] = useState<string | null>(null)
  const allocated = useMemo(
    () => allocations.reduce((sum, item) => sum + item.budgetMinor, 0), [allocations])
  const balanced = allocated === mix.totalBudgetMinor
  const scheduled = allocations.every(item => item.runningPeriods.length > 0 &&
    item.runningPeriods.every(period => periodSchema.safeParse(period).success))
  const editable = mix.status === masterDataCodes.lifecycleStatuses.draft

  function update(index: number, patch: Partial<MediaAllocation>) {
    setAllocations(current => current.map((item, itemIndex) =>
      itemIndex === index ? { ...item, ...patch } : item))
  }

  async function save() {
    setError(null)
    if (!balanced) { setError('Channel allocations must add up to the planning budget.'); return }
    if (!scheduled) { setError('Give every media type at least one valid running period.'); return }
    try { await onSave(allocations) } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The media mix could not be saved.')
    }
  }

  return <section className="planning-section" aria-labelledby="media-mix-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Media mix</p>
      <h2 id="media-mix-title">Shape the investment and timing</h2>
      <p>Change the budget, role and running periods for each media type before you confirm the mix.</p></div>
      <BudgetTotal allocated={allocated} total={mix.totalBudgetMinor} currency={mix.currency} /></div>
    <AllocationBars allocations={allocations} total={mix.totalBudgetMinor} currency={mix.currency} />
    <div className="media-allocation-grid">{allocations.map((allocation, index) =>
      <AllocationCard key={allocation.channel} allocation={allocation} currency={mix.currency}
        editable={editable} onChange={(patch) => update(index, patch)} />)}</div>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {editable ? <div className="planning-actions">
      <button className="secondary-button" type="button" disabled={busy} onClick={() => void save()}>
        {busy ? 'Saving…' : 'Save changes'}</button>
      <button className="primary-button" type="button" disabled={busy || !balanced || !scheduled}
        onClick={() => void onApprove()}>Confirm media mix</button>
    </div> : <div className="planning-confirmed"><span>Media mix confirmed.</span>
      <button className="secondary-button" type="button" disabled={busy}
        onClick={() => void onRevise()}>Revise media mix</button></div>}
  </section>
}

function AllocationBars({ allocations, total, currency }: {
  allocations: MediaAllocation[]; total: number; currency: string
}) {
  return <div className="allocation-bars" aria-label="Budget allocation by media type">
    {allocations.map(item => {
      const visual = mediaVisual(item.channel)
      const width = total === 0 ? 0 : Math.max(2, item.budgetMinor / total * 100)
      return <div className="allocation-bar-row" key={item.channel}>
        <div className={`media-identity media-tone-${visual.tone}`}><MediaTypeIcon channel={item.channel} />
          <span>{visual.label}</span></div>
        <div className="allocation-bar-track"><span className={`allocation-bar media-tone-${visual.tone}`}
          style={{ width: `${width}%` }} /></div>
        <strong>{formatMoney(item.budgetMinor, currency)}</strong>
      </div>
    })}
  </div>
}

function AllocationCard({ allocation, currency, editable, onChange }: {
  allocation: MediaAllocation
  currency: string
  editable: boolean
  onChange: (patch: Partial<MediaAllocation>) => void
}) {
  const visual = mediaVisual(allocation.channel)
  function updatePeriod(index: number, patch: Partial<RunningPeriod>) {
    onChange({ runningPeriods: allocation.runningPeriods.map((period, itemIndex) =>
      itemIndex === index ? { ...period, ...patch } : period) })
  }
  return <article className={`media-allocation-card media-tone-${visual.tone}`}>
    <header><div className="media-identity"><MediaTypeIcon channel={allocation.channel} />
      <div><h3>{visual.label}</h3><small>{allocation.channel}</small></div></div></header>
    <label>Budget <span>{currency}</span><input type="number" min="0" step="100"
      disabled={!editable} value={(allocation.budgetMinor / 100).toString()}
      onChange={(event) => onChange({ budgetMinor: Math.round(Number(event.target.value || 0) * 100) })} /></label>
    <label>Role in the plan<input type="text" disabled={!editable} value={allocation.role}
      onChange={(event) => onChange({ role: event.target.value })} /></label>
    <div className="running-period-editor"><div className="period-heading"><strong>Running periods</strong>
      {editable && <button className="text-action" type="button" onClick={() => onChange({
        runningPeriods: [...allocation.runningPeriods, emptyPeriod(allocation.runningPeriods)],
      })}>+ Add period</button>}</div>
      {allocation.runningPeriods.map((period, index) => <div className="period-row" key={`${index}-${period.start}`}>
        <label>Start<input type="date" disabled={!editable} value={period.start}
          onChange={(event) => updatePeriod(index, { start: event.target.value })} /></label>
        <label>End<input type="date" disabled={!editable} value={period.end}
          onChange={(event) => updatePeriod(index, { end: event.target.value })} /></label>
        {editable && allocation.runningPeriods.length > 1 && <button className="period-remove" type="button"
          aria-label={`Remove ${visual.label} running period ${index + 1}`} onClick={() => onChange({
            runningPeriods: allocation.runningPeriods.filter((_, itemIndex) => itemIndex !== index),
          })}>×</button>}
      </div>)}</div>
  </article>
}

function BudgetTotal({ allocated, total, currency }: { allocated: number; total: number; currency: string }) {
  const difference = total - allocated
  return <div className={`budget-total ${difference === 0 ? 'is-balanced' : 'is-unbalanced'}`}>
    <span>Allocated</span><strong>{formatMoney(allocated, currency)}</strong>
    <small>{difference === 0 ? 'Budget balanced' : `${formatMoney(Math.abs(difference), currency)} ${difference > 0 ? 'left' : 'over'}`}</small>
  </div>
}

function emptyPeriod(periods: RunningPeriod[]): RunningPeriod {
  const previous = periods.at(-1)
  return previous ? { start: previous.end, end: previous.end } : { start: '', end: '' }
}
