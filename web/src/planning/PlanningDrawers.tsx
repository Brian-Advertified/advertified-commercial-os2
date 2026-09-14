import { useMemo, useState } from 'react'
import type { MediaAllocation, MediaMix, Shortlist } from '../api/planning-schemas'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'

type Candidate = Shortlist['candidates'][number]
type FlightScope = 'channel' | 'missing'
type AlternativeSort = 'fit' | 'cost' | 'reach' | 'area'

const weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] as const
const dayparts = ['Breakfast', 'Drive Time', 'Prime Time', 'Always-on'] as const

export function FlightEditorDrawer({ allocation, mix, candidate, editable, busy, onClose, onCreateRevision, onSave }: {
  allocation: MediaAllocation
  mix: MediaMix
  candidate?: Candidate | null
  editable: boolean
  busy: boolean
  onClose: () => void
  onCreateRevision: () => Promise<void>
  onSave: (allocations: MediaAllocation[]) => Promise<void>
}) {
  const [periods, setPeriods] = useState(allocation.runningPeriods.map(item => ({ ...item })))
  const [scope, setScope] = useState<FlightScope>('channel')
  const [selectedWeekdays, setSelectedWeekdays] = useState<string[]>(
    allocation.schedule?.weekdays.length ? allocation.schedule.weekdays : weekdays.slice(0, 5))
  const [selectedDayparts, setSelectedDayparts] = useState<string[]>(allocation.schedule?.dayparts ?? [])
  const missing = mix.allocations.filter(item => item.channel !== allocation.channel && !item.runningPeriods.length)
  const schedule = { weekdays: selectedWeekdays, dayparts: selectedDayparts }

  const save = async () => {
    const allocations = mix.allocations.map(item => {
      const targeted = item.channel === allocation.channel || (scope === 'missing' && !item.runningPeriods.length)
      return targeted ? { ...item, runningPeriods: periods, schedule } : item
    })
    await onSave(allocations)
    onClose()
  }

  return <div className="connected-modal-backdrop" role="presentation" onMouseDown={event => {
    if (event.currentTarget === event.target) onClose()
  }}><section className="connected-side-drawer connected-flight-drawer" role="dialog" aria-modal="true"
    aria-labelledby="edit-flight-title">
    <FlightDrawerHeader allocation={allocation} candidate={candidate} onClose={onClose} />
    {!editable ? <LockedFlightRevision busy={busy} onCreateRevision={onCreateRevision} onClose={onClose} />
      : <FlightDrawerBody allocation={allocation} candidate={candidate} periods={periods} setPeriods={setPeriods}
        selectedWeekdays={selectedWeekdays} setSelectedWeekdays={setSelectedWeekdays}
        selectedDayparts={selectedDayparts} setSelectedDayparts={setSelectedDayparts}
        scope={scope} setScope={setScope} missing={missing} busy={busy} onClose={onClose} onSave={save} />}
  </section></div>
}

function FlightDrawerHeader({ allocation, candidate, onClose }: {
  allocation: MediaAllocation; candidate?: Candidate | null; onClose: () => void
}) {
  return <header className="connected-drawer-title"><div><h2 id="edit-flight-title">Edit flight</h2></div>
    <button className="connected-modal-close" type="button" onClick={onClose} aria-label="Close">×</button>
    <article className="connected-flight-placement-summary"><span className="connected-strategy-icon">
      <MediaTypeIcon channel={allocation.channel} /></span><div><strong>{mediaVisual(allocation.channel).label}{candidate
        ? ` — ${candidate.supplierName ?? 'Supplier'}` : ''}</strong><small>{candidate?.name ?? allocation.role}</small></div>
      <em>{candidate ? 'Selected placement' : 'Channel allocation'}</em></article>
  </header>
}

function FlightDrawerBody(props: {
  allocation: MediaAllocation
  candidate?: Candidate | null
  periods: MediaAllocation['runningPeriods']
  setPeriods: React.Dispatch<React.SetStateAction<MediaAllocation['runningPeriods']>>
  selectedWeekdays: string[]
  setSelectedWeekdays: React.Dispatch<React.SetStateAction<string[]>>
  selectedDayparts: string[]
  setSelectedDayparts: React.Dispatch<React.SetStateAction<string[]>>
  scope: FlightScope
  setScope: (scope: FlightScope) => void
  missing: MediaAllocation[]
  busy: boolean
  onClose: () => void
  onSave: () => Promise<void>
}) {
  const invalid = invalidPeriods(props.periods) || props.selectedWeekdays.length === 0
  return <div className="connected-flight-editor-body">
    <FlightPeriodEditor periods={props.periods} setPeriods={props.setPeriods} />
    <FlightScheduleEditor selectedWeekdays={props.selectedWeekdays} setSelectedWeekdays={props.setSelectedWeekdays}
      selectedDayparts={props.selectedDayparts} setSelectedDayparts={props.setSelectedDayparts} />
    <FlightApplyScope scope={props.scope} setScope={props.setScope} missing={props.missing} />
    <FlightImpactPreview allocation={props.allocation} candidate={props.candidate} />
    <div className="connected-flight-reconfirm"><span>!</span><p><strong>Supplier reconfirmation may be required.</strong>
      Date changes are retained as a planning revision and must be reconfirmed where supplier availability is time-sensitive.</p></div>
    <footer><button className="secondary-button" type="button" onClick={props.onClose}>Cancel</button>
      <button className="primary-button" type="button" disabled={props.busy || invalid}
        onClick={() => void props.onSave()}>{props.busy ? 'Saving…' : 'Apply new flight →'}</button></footer>
  </div>
}

function FlightPeriodEditor({ periods, setPeriods }: {
  periods: MediaAllocation['runningPeriods']
  setPeriods: React.Dispatch<React.SetStateAction<MediaAllocation['runningPeriods']>>
}) {
  return <section className="connected-flight-periods connected-flight-periods--reference">
    {periods.map((period, index) => <div key={`${period.start}-${period.end}-${index}`}>
      <label>Start date<input type="date" value={period.start}
        onChange={event => patchPeriod(setPeriods, index, { start: event.target.value })} /></label>
      <label>End date<input type="date" value={period.end}
        onChange={event => patchPeriod(setPeriods, index, { end: event.target.value })} /></label>
      <div className="connected-flight-weeks"><span>Weeks</span><strong>{flightWeeks(period.start, period.end)}</strong></div>
      {periods.length > 1 && <button type="button" className="text-action"
        onClick={() => setPeriods(current => current.filter((_, i) => i !== index))}>Remove</button>}
    </div>)}
    <button type="button" className="secondary-button connected-add-period" onClick={() => {
      const previous = periods.at(-1)
      setPeriods(current => [...current, { start: previous?.end ?? '', end: previous?.end ?? '' }])
    }}>+ Add flight period</button>
  </section>
}

function FlightScheduleEditor({ selectedWeekdays, setSelectedWeekdays, selectedDayparts, setSelectedDayparts }: {
  selectedWeekdays: string[]
  setSelectedWeekdays: React.Dispatch<React.SetStateAction<string[]>>
  selectedDayparts: string[]
  setSelectedDayparts: React.Dispatch<React.SetStateAction<string[]>>
}) {
  return <section className="connected-flight-schedule"><header><strong>Dayparts / Scheduling</strong>
    <small>Select when the activity should run. These settings are retained with this channel allocation.</small></header>
    <div className="connected-weekday-picker">{weekdays.map(day => <label key={day}><input type="checkbox"
      checked={selectedWeekdays.includes(day)} onChange={() => toggleValue(day, setSelectedWeekdays)} />{day}</label>)}</div>
    <div className="connected-daypart-picker">{dayparts.map(part => <label key={part}><input type="checkbox"
      checked={selectedDayparts.includes(part)} onChange={() => toggleValue(part, setSelectedDayparts)} />
      <span><strong>{part}</strong><small>{daypartTime(part)}</small></span></label>)}</div>
  </section>
}

function FlightApplyScope({ scope, setScope, missing }: {
  scope: FlightScope; setScope: (scope: FlightScope) => void; missing: MediaAllocation[]
}) {
  return <section className="connected-flight-scope"><header><strong>Apply changes to</strong></header>
    <label><input type="radio" checked={scope === 'channel'} onChange={() => setScope('channel')} />
      Apply to this channel allocation only</label>
    <label><input type="radio" checked={scope === 'missing'} disabled={!missing.length}
      onChange={() => setScope('missing')} />Apply to this channel and all channels without flight dates
      {missing.length > 0 && <small>{missing.map(item => mediaVisual(item.channel).label).join(', ')}</small>}</label>
  </section>
}

function FlightImpactPreview({ allocation, candidate }: { allocation: MediaAllocation; candidate?: Candidate | null }) {
  const assessment = candidate?.suitability?.buyAssessment
  return <section className="connected-flight-impact"><header><strong>Impact preview</strong>
    <small>The edit changes timing only; delivery impact is shown only where retained measurement evidence exists.</small></header>
    <div><Metric label="Allocated budget" value={formatMoney(allocation.budgetMinor, candidate?.currency ?? 'ZAR', 0)} />
      <Metric label="Measured impressions" value={compactNumber(assessment?.impressions)} />
      <Metric label="Measured reach" value={compactNumber(assessment?.reach)} /></div>
  </section>
}

function Metric({ label, value }: { label: string; value: string }) {
  return <span><small>{label}</small><strong>{value}</strong></span>
}

function LockedFlightRevision({ busy, onCreateRevision, onClose }: {
  busy: boolean; onCreateRevision: () => Promise<void>; onClose: () => void
}) {
  return <div className="connected-drawer-state"><Icon name="shield" /><div><h3>Create an editable revision first</h3>
    <p>The current media mix is approved. Advertified keeps approved truth immutable and creates a new planning version before dates can change.</p></div>
    <button className="primary-button" type="button" disabled={busy} onClick={() => void onCreateRevision().then(onClose)}>
      {busy ? 'Creating revision…' : 'Create media-plan revision'}</button></div>
}

export function ReplacementDrawer({ target, shortlist, busy, onClose, onStartRevision, onReplace }: {
  target: Candidate
  shortlist: Shortlist
  busy: boolean
  onClose: () => void
  onStartRevision: () => Promise<void>
  onReplace: (candidateIds: string[], reason: string) => Promise<void>
}) {
  const selectedIds = shortlist.candidates.filter(item => item.isSelected).map(item => item.id)
  const alternatives = shortlist.candidates.filter(item => item.isEligible && item.channel === target.channel &&
    item.id !== target.id && !selectedIds.includes(item.id))
  const editable = shortlist.status === masterDataCodes.lifecycleStatuses.draft
  const [sort, setSort] = useState<AlternativeSort>('fit')
  const [reason, setReason] = useState('Reviewing eligible alternatives for stronger campaign fit.')
  const ordered = useMemo(() => sortAlternatives(alternatives, sort, target), [alternatives, sort, target])
  const recommendation = ordered[0] ?? null
  const choose = async (replacement: Candidate) => {
    const nextIds = selectedIds.filter(id => id !== target.id).concat(replacement.id)
    await onReplace(nextIds, reason.trim() || `Replaced ${target.name} with ${replacement.name}.`)
    onClose()
  }
  return <div className="connected-modal-backdrop connected-replacement-backdrop" role="presentation" onMouseDown={event => {
    if (event.currentTarget === event.target) onClose()
  }}><section className="connected-side-drawer connected-replacement-drawer" role="dialog" aria-modal="true"
    aria-labelledby="replace-inventory-title"><header className="connected-drawer-title"><div>
      <h2 id="replace-inventory-title">Replace placement</h2><p>Find the best eligible alternative without leaving the campaign.</p></div>
      <button className="connected-modal-close" type="button" onClick={onClose} aria-label="Close">×</button></header>
    <CurrentPlacement target={target} reason={reason} setReason={setReason} />
    {editable ? <><AlternativeTabs value={sort} onChange={setSort} />
      <AlternativeList alternatives={ordered} busy={busy} onChoose={choose} />
      {recommendation && <ReplacementComparison target={target} alternative={recommendation} busy={busy}
        onChoose={() => choose(recommendation)} />}</>
      : <LockedReplacement busy={busy} onStartRevision={onStartRevision} onClose={onClose} />}
  </section></div>
}

function CurrentPlacement({ target, reason, setReason }: { target: Candidate; reason: string; setReason: (value: string) => void }) {
  return <><article className="connected-current-placement connected-current-placement--reference">
    <span className="connected-strategy-icon"><MediaTypeIcon channel={target.channel} /></span><div><small>Current placement</small>
      <strong>{target.name}</strong><span>{target.supplierName ?? 'Supplier'} · {mediaVisual(target.channel).label}</span></div>
    <Metric label="Rate" value={candidateRate(target)} /><Metric label="Est. impressions" value={compactNumber(target.suitability?.buyAssessment?.impressions)} />
  </article>
  <label className="connected-replacement-reason"><span>Reason for replacement</span>
    <input value={reason} onChange={event => setReason(event.target.value)} /></label></>
}

function AlternativeTabs({ value, onChange }: { value: AlternativeSort; onChange: (value: AlternativeSort) => void }) {
  const tabs: Array<[AlternativeSort, string]> = [['fit', 'Better Fit'], ['cost', 'Lower Cost'], ['reach', 'Similar Reach'], ['area', 'Same Area']]
  return <div className="connected-alternative-tabs">{tabs.map(([key, label]) => <button key={key} type="button"
    className={value === key ? 'is-active' : ''} onClick={() => onChange(key)}>{label}</button>)}</div>
}

function LockedReplacement({ busy, onStartRevision, onClose }: {
  busy: boolean; onStartRevision: () => Promise<void>; onClose: () => void
}) {
  return <div className="connected-drawer-state"><Icon name="inventory" /><div><h3>Start a replacement review</h3>
    <p>The current shortlist is confirmed. Advertified creates a new shortlist version before a replacement can be selected.</p></div>
    <button className="primary-button" type="button" disabled={busy}
      onClick={() => void onStartRevision().then(onClose)}>{busy ? 'Creating review…' : 'Create replacement review'}</button></div>
}

function AlternativeList({ alternatives, busy, onChoose }: {
  alternatives: Candidate[]; busy: boolean; onChoose: (candidate: Candidate) => Promise<void>
}) {
  if (!alternatives.length) return <div className="connected-empty-alternatives"><Icon name="search" />
    <h3>No eligible alternatives</h3><p>No current candidate in this channel satisfies the governed eligibility rules.</p></div>
  return <div className="connected-alternative-list connected-alternative-list--reference">{alternatives.map(item => <article key={item.id}>
    <span className="connected-alternative-visual"><MediaTypeIcon channel={item.channel} /></span><div className="connected-alternative-copy">
      <strong>{item.name}</strong><small>{item.supplierName ?? 'Supplier'} · {item.geography}</small>
      <div><b>{candidateRate(item)}</b><span>{compactNumber(item.suitability?.buyAssessment?.impressions)} impressions</span>
        <span>{item.availabilityId ? 'Availability evidence retained' : 'Availability needs confirmation'}</span></div></div>
    <em>{replacementFit(item)} fit</em><button className="primary-button" type="button" disabled={busy}
      onClick={() => void onChoose(item)}>Replace with this</button>
  </article>)}</div>
}

function ReplacementComparison({ target, alternative, busy, onChoose }: {
  target: Candidate; alternative: Candidate; busy: boolean; onChoose: () => Promise<void>
}) {
  return <section className="connected-replacement-comparison"><div><small>Current placement</small><strong>{target.name}</strong>
    <span>{candidateRate(target)} · {replacementFit(target)}% fit</span></div><b>→</b><div><small>Recommended alternative</small>
      <strong>{alternative.name}</strong><span>{candidateRate(alternative)} · {replacementFit(alternative)}% fit</span></div>
    <div className="connected-replacement-difference"><small>Difference</small><strong>{rateDifference(target, alternative)}</strong>
      <span>{fitDifference(target, alternative)}</span></div>
    <footer><span className="connected-ai-orb">✦</span><p><strong>AI recommendation</strong>
      The first option is ranked from the retained fit, rate, reach and geography evidence available in this shortlist.</p>
      <button className="secondary-button" type="button" disabled={busy} onClick={() => void onChoose()}>Use this recommendation</button></footer>
  </section>
}

function sortAlternatives(values: Candidate[], sort: AlternativeSort, target: Candidate) {
  return [...values].sort((a, b) => alternativeScore(b, sort, target) - alternativeScore(a, sort, target))
}

function alternativeScore(candidate: Candidate, sort: AlternativeSort, target: Candidate) {
  const scorers: Record<AlternativeSort, () => number> = {
    cost: () => -(candidate.rateAmountMinor ?? Number.MAX_SAFE_INTEGER),
    reach: () => -reachDistance(candidate, target),
    area: () => areaScore(candidate, target),
    fit: () => fitScore(candidate),
  }
  return scorers[sort]()
}

function reachDistance(candidate: Candidate, target: Candidate) {
  const candidateReach = candidate.suitability?.buyAssessment?.reach ?? 0
  const targetReach = target.suitability?.buyAssessment?.reach ?? 0
  return Math.abs(candidateReach - targetReach)
}

function areaScore(candidate: Candidate, target: Candidate) {
  return candidate.geography === target.geography ? 1_000_000 + fitScore(candidate) : fitScore(candidate)
}

function fitScore(candidate: Candidate) {
  const fit = candidate.suitability?.total ?? candidate.score
  return fit == null ? 0 : fit * (fit <= 1 ? 100 : 1)
}

function candidateRate(candidate: Candidate) {
  if (candidate.rateAmountMinor == null) return 'Not supplied'
  return formatMoney(candidate.rateAmountMinor, candidate.currency ?? masterDataCodes.currencies.zar, 0)
}

function replacementFit(candidate: Candidate) {
  return Math.round(fitScore(candidate)).toString()
}

function rateDifference(target: Candidate, alternative: Candidate) {
  if (target.rateAmountMinor == null || alternative.rateAmountMinor == null) return 'Rate comparison unavailable'
  const difference = alternative.rateAmountMinor - target.rateAmountMinor
  const sign = difference > 0 ? '+' : difference < 0 ? '−' : ''
  return `${sign}${formatMoney(Math.abs(difference), alternative.currency ?? target.currency ?? 'ZAR', 0)}`
}

function fitDifference(target: Candidate, alternative: Candidate) {
  const delta = Math.round(fitScore(alternative) - fitScore(target))
  return `${delta >= 0 ? '+' : ''}${delta}% audience fit`
}

function patchPeriod(setPeriods: React.Dispatch<React.SetStateAction<MediaAllocation['runningPeriods']>>, index: number,
  patch: Partial<MediaAllocation['runningPeriods'][number]>) {
  setPeriods(current => current.map((item, i) => i === index ? { ...item, ...patch } : item))
}

function toggleValue(value: string, setValues: React.Dispatch<React.SetStateAction<string[]>>) {
  setValues(current => current.includes(value) ? current.filter(item => item !== value) : [...current, value])
}

function invalidPeriods(periods: MediaAllocation['runningPeriods']) {
  return periods.length === 0 || periods.some(item => !item.start || !item.end || item.end < item.start)
}

function flightWeeks(start: string, end: string) {
  if (!start || !end || end < start) return '—'
  const days = Math.floor((Date.parse(`${end}T00:00:00Z`) - Date.parse(`${start}T00:00:00Z`)) / 86_400_000) + 1
  return `${Math.max(1, Math.ceil(days / 7))}`
}

function daypartTime(part: string) {
  if (part === 'Breakfast') return '06:00–09:00'
  if (part === 'Drive Time') return '16:00–19:00'
  if (part === 'Prime Time') return '19:00–22:00'
  return '00:00–24:00'
}

function compactNumber(value: number | null | undefined) {
  if (value == null) return 'Not modelled'
  if (value >= 1_000_000) return `${Math.round(value / 100_000) / 10}M`
  if (value >= 1_000) return `${Math.round(value / 100) / 10}K`
  return Math.round(value).toLocaleString('en-ZA')
}
