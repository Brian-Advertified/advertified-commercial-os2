import { useMemo, useState } from 'react'
import { z } from 'zod'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import type { MediaAllocation, MediaMix, RunningPeriod } from '../api/planning-schemas'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, majorAmountToMinor, minorAmountToInput } from '../presentation/format'
import { mediaVisual } from './media-visuals'

const periodSchema = z.object({ start: z.iso.date(), end: z.iso.date() })
  .refine((value) => value.end >= value.start, { message: 'End date must be after start date.' })

type MediaMixEditorProps = {
  mix: MediaMix
  allowedChannels: string[]
  busy: boolean
  onSave: (allocations: MediaAllocation[]) => Promise<void>
  onApprove: () => Promise<void>
  onRevise: () => Promise<void>
}

export function MediaMixEditor(props: MediaMixEditorProps) {
  const draft = useMediaMixDraft(props)
  const editable = props.mix.status === masterDataCodes.lifecycleStatuses.draft
  return <section className="planning-section" aria-labelledby="media-mix-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Media mix</p>
      <h2 id="media-mix-title">Shape the investment and timing</h2>
      <p>Add or remove permitted media types, then reconcile each budget, role and running period before confirming the mix.</p></div>
      <BudgetTotal allocated={draft.allocated} total={props.mix.totalBudgetMinor}
        currency={props.mix.currency} /></div>
    <AllocationBars allocations={draft.allocations} total={props.mix.totalBudgetMinor}
      currency={props.mix.currency} />
    {editable && draft.unusedChannels.length > 0 && <div className="media-channel-picker">
      <label>Add media type<select value={draft.channelToAdd}
        onChange={(event) => draft.setChannelToAdd(event.target.value)}>
        <option value="">Choose a permitted channel</option>
        {draft.unusedChannels.map(channel =>
          <option key={channel} value={channel}>{channel}</option>)}
      </select></label>
      <button className="secondary-button" type="button" disabled={!draft.channelToAdd}
        onClick={draft.addChannel}>Add media type</button>
    </div>}
    <div className="media-allocation-grid">{draft.allocations.map((allocation, index) =>
      <AllocationCard key={allocation.channel} allocation={allocation}
        currency={props.mix.currency} editable={editable}
        canRemove={draft.allocations.length > 1}
        onRemove={() => draft.removeChannel(index)}
        onChange={(patch) => draft.update(index, patch)} />)}</div>
    {draft.error && <p className="inline-alert" role="alert">{draft.error}</p>}
    {editable ? <div className="planning-actions">
      <button className="secondary-button" type="button" disabled={props.busy}
        onClick={() => void draft.save()}>
        {props.busy ? 'Saving…' : 'Save changes'}</button>
      <button className="primary-button" type="button"
        disabled={props.busy || !draft.balanced || !draft.scheduled}
        onClick={() => void props.onApprove()}>Confirm media mix</button>
    </div> : <div className="planning-confirmed"><span>Media mix confirmed.</span>
      <button className="secondary-button" type="button" disabled={props.busy}
        onClick={() => void props.onRevise()}>Revise media mix</button></div>}
  </section>
}

function useMediaMixDraft({ mix, allowedChannels, onSave }: MediaMixEditorProps) {
  const [allocations, setAllocations] = useState<MediaAllocation[]>(mix.allocations)
  const [channelToAdd, setChannelToAdd] = useState('')
  const [error, setError] = useState<string | null>(null)
  const allocated = useMemo(
    () => allocations.reduce((sum, item) => sum + item.budgetMinor, 0), [allocations])
  const balanced = allocated === mix.totalBudgetMinor
  const scheduled = allocations.every(item => item.runningPeriods.length > 0 &&
    item.runningPeriods.every(period => periodSchema.safeParse(period).success))
  const unusedChannels = allowedChannels.filter(channel =>
    !allocations.some(item => item.channel === channel))

  function update(index: number, patch: Partial<MediaAllocation>) {
    setAllocations(current => current.map((item, itemIndex) =>
      itemIndex === index ? { ...item, ...patch } : item))
  }
  function addChannel() {
    if (!channelToAdd || !unusedChannels.includes(channelToAdd)) return
    const periods = allocations[0]?.runningPeriods.map(period => ({ ...period })) ?? []
    setAllocations(current => [...current, {
      channel: channelToAdd, budgetMinor: 0, role: 'Supporting channel',
      runningPeriods: periods.length > 0 ? periods : [{ start: '', end: '' }],
    }])
    setChannelToAdd('')
  }
  function removeChannel(index: number) {
    setAllocations(current => current.filter((_, itemIndex) => itemIndex !== index))
  }
  async function save() {
    setError(null)
    if (!balanced) { setError('Channel allocations must add up to the planning budget.'); return }
    if (!scheduled) { setError('Give every media type at least one valid running period.'); return }
    try { await onSave(allocations) } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The media mix could not be saved.')
    }
  }
  return { allocations, channelToAdd, setChannelToAdd, error, allocated, balanced,
    scheduled, unusedChannels, update, addChannel, removeChannel, save }
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

function AllocationCard({
  allocation,
  currency,
  editable,
  canRemove,
  onRemove,
  onChange,
}: {
  allocation: MediaAllocation
  currency: string
  editable: boolean
  canRemove: boolean
  onRemove: () => void
  onChange: (patch: Partial<MediaAllocation>) => void
}) {
  const visual = mediaVisual(allocation.channel)
  function updatePeriod(index: number, patch: Partial<RunningPeriod>) {
    onChange({ runningPeriods: allocation.runningPeriods.map((period, itemIndex) =>
      itemIndex === index ? { ...period, ...patch } : period) })
  }
  return <article className={`media-allocation-card media-tone-${visual.tone}`}>
    <header><div className="media-identity"><MediaTypeIcon channel={allocation.channel} />
      <div><h3>{visual.label}</h3><small>{allocation.channel}</small></div></div>
      {editable && canRemove && <button className="text-action" type="button"
        aria-label={`Remove ${visual.label} from media mix`}
        onClick={onRemove}>Remove</button>}</header>
    <label>Budget <span>{currency}</span><input type="number" min="0" step="any"
      disabled={!editable} value={minorAmountToInput(allocation.budgetMinor, currency)}
      onChange={(event) => onChange({
        budgetMinor: majorAmountToMinor(Number(event.target.value || 0), currency),
      })} /></label>
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
