import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { mediaStrategyApi, type MediaStrategyRecord } from '../api/media-strategy-client'
import { planningApi } from '../api/planning-client'
import type {
  MediaAllocation,
  MediaGeographyAllocation,
  MediaImpactEstimate,
  MediaMix,
  PlanningWorkspace,
} from '../api/planning-schemas'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, majorAmountToMinor, minorAmountToInput } from '../presentation/format'
import { mediaVisual } from './media-visuals'
import { MediaStrategyPreparation } from './MediaStrategyPreparation'
import { FlightEditorDrawer } from './PlanningDrawers'
import {
  allocationPeriods,
  campaignModeLabel,
  isApproved,
  strategyExplanation,
} from './planning-presentation'

type ActionRunner = (action: () => Promise<unknown>) => Promise<void>
type DisplayMode = 'percentage' | 'value'

type Props = {
  tenantId: string
  briefVersionId: string
  token: string
  workspace: PlanningWorkspace
  busy: boolean
  error: string | null
  act: ActionRunner
  mix: MediaMix | null
}

export function ConnectedStrategyView(props: Props) {
  return <section className="connected-strategy-page">
    <StrategyHeading />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {props.mix ? <StrategyContent {...props} mix={props.mix} />
      : <MediaStrategyPreparation key={`${props.tenantId}:${props.briefVersionId}`}
        {...props} briefId={props.workspace.briefId} />}
  </section>
}

function StrategyHeading() {
  return <header className="connected-stage-heading connected-strategy-heading"><div><p className="eyebrow">New campaign</p>
    <h1>Strategy recommendations</h1>
    <p>Here’s your recommended media strategy, built from the approved brief, audience, evidence and planning budget.</p></div>
    <div className="connected-handwritten-note" aria-hidden="true">Turn ideas<br />into impact.<span /></div>
  </header>
}

function StrategyContent(props: Props & { mix: MediaMix }) {
  const strategy = useLatestStrategy(props.tenantId, props.briefVersionId, props.mix.mediaStrategyArtifactId)
  return <div className="connected-strategy-grid connected-strategy-dashboard">
    <div className="connected-strategy-main-column">
      <StrategyMainCard key={`${props.mix.id}:${props.mix.version}`} {...props} strategy={strategy} />
      <div className="connected-strategy-support-grid">
        <StrategyTimeline mix={props.mix} />
        <StrategyGeography workspace={props.workspace} mix={props.mix} strategy={strategy} />
      </div>
      <StrategyFooter {...props} />
    </div>
    <StrategySidebar {...props} strategy={strategy} />
  </div>
}

function useLatestStrategy(tenantId: string, briefVersionId: string, strategyArtifactId: string | null) {
  const [strategy, setStrategy] = useState<MediaStrategyRecord | null>(null)
  useEffect(() => {
    let active = true
    void mediaStrategyApi.getLatest(tenantId, briefVersionId)
      .then(value => { if (active) setStrategy(value) })
      .catch(() => { if (active) setStrategy(null) })
    return () => { active = false }
  }, [tenantId, briefVersionId, strategyArtifactId])
  return strategy
}

function StrategyMainCard(props: Props & { mix: MediaMix; strategy: MediaStrategyRecord | null }) {
  const [flightChannel, setFlightChannel] = useState<string | null>(null)
  const [geographyChannel, setGeographyChannel] = useState<string | null>(null)
  const [displayMode, setDisplayMode] = useState<DisplayMode>('percentage')
  const [editing, setEditing] = useState(false)
  const [pending, setPending] = useState<Record<string, number>>(() => allocationPercentMap(props.mix))
  const editable = props.mix.status === masterDataCodes.lifecycleStatuses.draft
  const pendingTotal = totalPercent(pending)
  const validAllocation = Math.abs(pendingTotal - 100) < 0.05
  const flight = props.mix.allocations.find(item => item.channel === flightChannel)
  const geography = props.mix.allocations.find(item => item.channel === geographyChannel)

  async function saveAllocations() {
    if (!validAllocation) return
    await props.act(() => planningApi.updateMix(
      props.tenantId, props.mix, allocationsFromPercentages(props.mix, pending), props.token))
    setEditing(false)
  }

  async function saveGeography(geographyAllocations: MediaGeographyAllocation[]) {
    if (!geography) return
    const allocations = props.mix.allocations.map(item => item.channel === geography.channel
      ? { ...item, geographyAllocations }
      : item)
    await props.act(() => planningApi.updateMix(props.tenantId, props.mix, allocations, props.token))
    setGeographyChannel(null)
  }

  function setBudget(channel: string, budgetMinor: number) {
    const percent = props.mix.totalBudgetMinor > 0 ? budgetMinor / props.mix.totalBudgetMinor * 100 : 0
    setPending(current => ({ ...current, [channel]: clampPercent(percent) }))
  }

  return <><section className="connected-strategy-main-card connected-strategy-mix-card">
    <StrategyMixHeader editable={editable} editing={editing} displayMode={displayMode} busy={props.busy}
      onToggleEdit={() => setEditing(value => !value)} onDisplayMode={setDisplayMode}
      onRevise={() => props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))} />
    <AllocationStrip mix={props.mix} displayMode={displayMode} pending={editing ? pending : null} />
    <AllocationLegend mix={props.mix} />
    <StrategyAllocationTable workspace={props.workspace} mix={props.mix} strategy={props.strategy}
      editing={editing} pending={pending} onPercentChange={(channel, value) =>
        setPending(current => ({ ...current, [channel]: clampPercent(value) }))}
      onBudgetChange={setBudget}
      onEditFlight={editable ? setFlightChannel : undefined}
      onEditGeography={editable ? setGeographyChannel : undefined} />
    <AllocationEditFooter editing={editing} valid={validAllocation} total={pendingTotal}
      busy={props.busy} onSave={saveAllocations} />
    <StrategyFlightWarning mix={props.mix} />
    <StrategyGeographyWarning workspace={props.workspace} mix={props.mix} />
  </section>
  <StrategyFlightEditor {...props} flight={flight} editable={editable} onClose={() => setFlightChannel(null)} />
  {geography && <GeographyAllocationEditor allocation={geography} markets={campaignMarkets(props.workspace)}
    currency={props.mix.currency} busy={props.busy} onClose={() => setGeographyChannel(null)} onSave={saveGeography} />}</>
}

function StrategyMixHeader({ editable, editing, displayMode, busy, onToggleEdit, onDisplayMode, onRevise }: {
  editable: boolean
  editing: boolean
  displayMode: DisplayMode
  busy: boolean
  onToggleEdit: () => void
  onDisplayMode: (value: DisplayMode) => void
  onRevise: () => Promise<void>
}) {
  return <header><div className="connected-strategy-card-title"><span className="connected-strategy-icon"><Icon name="chart" /></span><div>
    <h2>Recommended media mix</h2><p>Budget, channel role and flighting are tied to the current approved planning lineage.</p></div></div>
    <div className="connected-strategy-header-actions">
      {editable
        ? <button className="text-action" type="button" onClick={onToggleEdit}>
          ✎ {editing ? 'Cancel editing' : 'Edit allocations'}</button>
        : <button className="text-action" type="button" disabled={busy} onClick={() => void onRevise()}>
          ↻ {busy ? 'Creating revision…' : 'Revise strategy'}</button>}
      <div className="connected-strategy-view-toggle" aria-label="Media mix display">
        <span>View as</span><button type="button" className={displayMode === 'percentage' ? 'is-active' : ''}
          onClick={() => onDisplayMode('percentage')}>Percentage</button>
        <button type="button" className={displayMode === 'value' ? 'is-active' : ''}
          onClick={() => onDisplayMode('value')}>Rand value</button>
      </div>
    </div></header>
}

function AllocationEditFooter({ editing, valid, total, busy, onSave }: {
  editing: boolean
  valid: boolean
  total: number
  busy: boolean
  onSave: () => Promise<void>
}) {
  if (!editing) return null
  const message = valid ? 'Allocation totals 100%.' : `Allocation totals ${total.toFixed(1)}%. It must equal 100%.`
  return <div className={`connected-strategy-edit-footer${valid ? '' : ' is-invalid'}`}>
    <span>{message}</span><button className="primary-button" type="button" disabled={busy || !valid}
      onClick={() => void onSave()}>{busy ? 'Saving allocation…' : 'Save allocations'}</button>
  </div>
}

function StrategyFlightWarning({ mix }: { mix: MediaMix }) {
  if (strategyFlightsReady(mix)) return null
  return <p className="inline-notice connected-strategy-flight-warning" role="status">
    Set and save flight dates for every channel before approving the strategy.</p>
}

function StrategyGeographyWarning({ workspace, mix }: { workspace: PlanningWorkspace; mix: MediaMix }) {
  const markets = campaignMarkets(workspace)
  if (strategyGeographiesReady(mix, markets)) return null
  return <p className="inline-notice connected-strategy-geography-warning" role="status">
    Allocate each funded media channel across the approved campaign markets before approving the strategy.</p>
}

function StrategyFlightEditor(props: Props & {
  mix: MediaMix
  flight: MediaAllocation | undefined
  editable: boolean
  onClose: () => void
}) {
  if (!props.flight) return null
  return <FlightEditorDrawer allocation={props.flight} mix={props.mix} editable={props.editable} busy={props.busy}
    onClose={props.onClose}
    onCreateRevision={() => props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))}
    onSave={allocations => props.act(() => planningApi.updateMix(props.tenantId, props.mix, allocations, props.token))} />
}

function GeographyAllocationEditor({ allocation, markets, currency, busy, onClose, onSave }: {
  allocation: MediaAllocation
  markets: string[]
  currency: string
  busy: boolean
  onClose: () => void
  onSave: (values: MediaGeographyAllocation[]) => Promise<void>
}) {
  const [values, setValues] = useState<Record<string, number>>(() => Object.fromEntries(
    markets.map(market => [market, allocation.geographyAllocations.find(item => item.geography === market)?.budgetMinor ?? 0])))
  const total = Object.values(values).reduce((sum, value) => sum + value, 0)
  const balanced = total === allocation.budgetMinor && Object.values(values).some(value => value > 0)
  function splitEvenly() {
    setValues(Object.fromEntries(splitBudgetEvenly(allocation.budgetMinor, markets).map(item => [item.geography, item.budgetMinor])))
  }
  async function save() {
    if (!balanced) return
    await onSave(markets.map(geography => ({ geography, budgetMinor: values[geography] ?? 0 }))
      .filter(item => item.budgetMinor > 0))
  }
  return <div className="connected-strategy-drawer-backdrop"><section className="connected-strategy-drawer" role="dialog"
    aria-modal="true" aria-label={`Geography allocation for ${mediaVisual(allocation.channel).label}`}>
    <header><div><p className="eyebrow">Geographic allocation</p><h2>{mediaVisual(allocation.channel).label}</h2>
      <p>Split this channel's {formatMoney(allocation.budgetMinor, currency, 0)} across approved campaign markets.</p></div>
      <button type="button" className="text-action" onClick={onClose}>Close</button></header>
    <div className="connected-strategy-geo-editor-list">{markets.map(market => {
      const budget = values[market] ?? 0
      const percent = allocation.budgetMinor > 0 ? budget / allocation.budgetMinor * 100 : 0
      return <label key={market}><span><strong>{market}</strong><small>{round1(percent)}%</small></span>
        <span className="connected-strategy-budget-input"><span>{currency}</span><input type="number" min="0" step="any"
          aria-label={`${market} ${mediaVisual(allocation.channel).label} budget`}
          value={minorAmountToInput(budget, currency)} onChange={event => setValues(current => ({ ...current,
            [market]: majorAmountToMinor(Number(event.target.value || 0), currency),
          }))} /></span></label>
    })}</div>
    <div className={`connected-strategy-drawer-total${balanced ? '' : ' is-invalid'}`}><span>Allocated</span>
      <strong>{formatMoney(total, currency, 0)} / {formatMoney(allocation.budgetMinor, currency, 0)}</strong></div>
    <footer><button className="secondary-button" type="button" disabled={busy || markets.length === 0}
      onClick={splitEvenly}>Split evenly</button><div><button className="secondary-button" type="button" onClick={onClose}>Cancel</button>
      <button className="primary-button" type="button" disabled={busy || !balanced} onClick={() => void save()}>
        {busy ? 'Saving markets…' : 'Save geography allocation'}</button></div></footer>
  </section></div>
}

function StrategyAllocationTable(props: {
  workspace: PlanningWorkspace
  mix: MediaMix
  strategy: MediaStrategyRecord | null
  editing: boolean
  pending: Record<string, number>
  onPercentChange: (channel: string, value: number) => void
  onBudgetChange: (channel: string, budgetMinor: number) => void
  onEditFlight?: (channel: string) => void
  onEditGeography?: (channel: string) => void
}) {
  return <div className="connected-strategy-table"><div className="connected-strategy-table-head">
    <span>Media channel</span><span>Allocation</span><span>Budget</span><span>Flighting period</span><span>Geography</span><span>Actions</span></div>
    {props.mix.allocations.map(item => <StrategyAllocationRow key={item.channel} {...props} item={item} />)}
    <div className="connected-strategy-total-row"><strong>Total</strong><strong>100%</strong>
      <strong>{formatMoney(props.mix.totalBudgetMinor, props.mix.currency, 0)}</strong><span /><span /><span /></div>
  </div>
}

function StrategyAllocationRow(props: {
  workspace: PlanningWorkspace
  mix: MediaMix
  strategy: MediaStrategyRecord | null
  editing: boolean
  pending: Record<string, number>
  onPercentChange: (channel: string, value: number) => void
  onBudgetChange: (channel: string, budgetMinor: number) => void
  onEditFlight?: (channel: string) => void
  onEditGeography?: (channel: string) => void
  item: MediaAllocation
}) {
  const percent = props.pending[props.item.channel] ?? allocationPercentNumber(props.item, props.mix)
  const budget = rowBudget(props.mix, props.item, percent, props.editing)
  return <div><MediaChannelCell item={props.item} />
    <StrategyAllocationValue item={props.item} editing={props.editing} percent={percent}
      onPercentChange={props.onPercentChange} />
    <StrategyBudgetValue item={props.item} mix={props.mix} editing={props.editing} budget={budget}
      onBudgetChange={props.onBudgetChange} />
    <StrategyFlightCell item={props.item} onEditFlight={props.onEditFlight} />
    <StrategyGeographyCell workspace={props.workspace} strategy={props.strategy} item={props.item}
      onEditGeography={props.onEditGeography} />
    <StrategyRowAction item={props.item} onEditFlight={props.onEditFlight} /></div>
}

function MediaChannelCell({ item }: { item: MediaAllocation }) {
  return <span className="connected-media-cell"><span className="connected-media-dot"
    style={{ background: mediaVisual(item.channel).color }} /><MediaTypeIcon channel={item.channel} />
    <strong>{mediaVisual(item.channel).label}</strong></span>
}

function StrategyAllocationValue({ item, editing, percent, onPercentChange }: {
  item: MediaAllocation
  editing: boolean
  percent: number
  onPercentChange: (channel: string, value: number) => void
}) {
  if (!editing) return <span>{round1(percent)}%</span>
  return <span><label className="connected-strategy-percent-input">
    <input aria-label={`${mediaVisual(item.channel).label} allocation percentage`} type="number"
      min="0" max="100" step="0.1" value={round1(percent)}
      onChange={event => onPercentChange(item.channel, Number(event.target.value))} /><b>%</b>
  </label></span>
}

function StrategyBudgetValue({ item, mix, editing, budget, onBudgetChange }: {
  item: MediaAllocation
  mix: MediaMix
  editing: boolean
  budget: number
  onBudgetChange: (channel: string, budgetMinor: number) => void
}) {
  if (!editing) return <strong>{formatMoney(budget, mix.currency, 0)}</strong>
  return <span><label className="connected-strategy-budget-input"><span>{mix.currency}</span>
    <input aria-label={`${mediaVisual(item.channel).label} budget`} type="number" min="0" step="any"
      value={minorAmountToInput(budget, mix.currency)} onChange={event => onBudgetChange(
        item.channel, majorAmountToMinor(Number(event.target.value || 0), mix.currency))} />
  </label></span>
}

function StrategyGeographyCell({ workspace, strategy, item, onEditGeography }: {
  workspace: PlanningWorkspace
  strategy: MediaStrategyRecord | null
  item: MediaAllocation
  onEditGeography?: (channel: string) => void
}) {
  const summary = geographyAllocationSummary(item)
    || strategyGeography(workspace, strategy, item.channel)
  if (!onEditGeography) return <span className="connected-strategy-geography-cell" title={summary}>{summary}</span>
  return <span className="connected-strategy-geography-cell"><button type="button"
    onClick={() => onEditGeography(item.channel)} aria-label={`Edit geography allocation for ${mediaVisual(item.channel).label}`}>
    {summary || 'Set market allocation'}</button></span>
}

function geographyAllocationSummary(item: MediaAllocation) {
  if (!item.geographyAllocations.length || item.budgetMinor <= 0) return ''
  return item.geographyAllocations.filter(value => value.budgetMinor > 0).map(value =>
    `${value.geography} ${round1(value.budgetMinor / item.budgetMinor * 100)}%`).join(' · ')
}

function StrategyFlightCell({ item, onEditFlight }: {
  item: MediaAllocation
  onEditFlight?: (channel: string) => void
}) {
  if (!onEditFlight) return <span>{allocationPeriods(item)}</span>
  const label = item.runningPeriods.length ? allocationPeriods(item) : 'Set flight dates'
  return <span><button className="connected-strategy-flight-button" type="button"
    onClick={() => onEditFlight(item.channel)} aria-label={`Edit flight dates for ${mediaVisual(item.channel).label}`}>
    <Icon name="calendar" /> {label}</button></span>
}

function StrategyRowAction({ item, onEditFlight }: {
  item: MediaAllocation
  onEditFlight?: (channel: string) => void
}) {
  if (!onEditFlight) return <span className="connected-strategy-row-actions">—</span>
  return <span className="connected-strategy-row-actions"><button type="button"
    aria-label={`Edit ${mediaVisual(item.channel).label} strategy row`} onClick={() => onEditFlight(item.channel)}>⋮</button></span>
}

function strategyGeography(workspace: PlanningWorkspace, strategy: MediaStrategyRecord | null, channel: string) {
  const recommendation = strategy?.details.channelRecommendations.find(item => item.channel === channel)
  return recommendation?.geographyRole || campaignMarkets(workspace).join(' · ') || 'Not established'
}

function rowBudget(mix: MediaMix, item: MediaAllocation, percent: number, editing: boolean) {
  return editing ? Math.round(mix.totalBudgetMinor * percent / 100) : item.budgetMinor
}

function AllocationStrip({ mix, displayMode, pending }: {
  mix: MediaMix
  displayMode: DisplayMode
  pending: Record<string, number> | null
}) {
  const total = mix.totalBudgetMinor || 1
  return <div className="connected-allocation-strip">{mix.allocations.map(item => {
    const percent = pending?.[item.channel] ?? item.budgetMinor / total * 100
    const value = pending ? Math.round(mix.totalBudgetMinor * percent / 100) : item.budgetMinor
    return <div key={item.channel} style={{ width: `${Math.max(4, percent)}%`, background: mediaVisual(item.channel).color }}>
      <strong>{displayMode === 'percentage' ? `${round1(percent)}%` : formatMoney(value, mix.currency, 0)}</strong>
    </div>
  })}</div>
}

function AllocationLegend({ mix }: { mix: MediaMix }) {
  return <div className="connected-strategy-legend">{mix.allocations.map(item => <span key={item.channel}>
    <i style={{ background: mediaVisual(item.channel).color }} />{mediaVisual(item.channel).label}</span>)}</div>
}

function StrategyTimeline({ mix }: { mix: MediaMix }) {
  const rows = timelineRows(mix)
  const bounds = timelineBounds(rows)
  return <section className="connected-strategy-support-card connected-strategy-timeline"><header>
    <span className="connected-strategy-icon"><Icon name="calendar" /></span><div><h2>Campaign flighting timeline</h2>
      <p>Persisted channel flight dates from the current strategy.</p></div></header>
    {rows.length && bounds ? <div className="connected-strategy-timeline-body">
      <div className="connected-strategy-months">{timelineMonths(bounds.start, bounds.end).map(month =>
        <span key={month}>{month}</span>)}</div>
      {rows.map(row => <div className="connected-strategy-timeline-row" key={row.channel}>
        <span>{mediaVisual(row.channel).label}</span><div><i style={{
          left: `${timelineOffset(row.start, bounds.start, bounds.end)}%`,
          width: `${timelineWidth(row.start, row.end, bounds.start, bounds.end)}%`,
          background: mediaVisual(row.channel).color,
        }} /></div></div>)}
    </div> : <p className="connected-strategy-empty-copy">Flight dates have not been retained for this allocation yet.</p>}
  </section>
}

function StrategyGeography({ workspace, mix, strategy }: {
  workspace: PlanningWorkspace
  mix: MediaMix
  strategy: MediaStrategyRecord | null
}) {
  const markets = campaignMarkets(workspace)
  const marketBudgets = aggregateGeographyBudgets(mix)
  const allocated = [...marketBudgets.values()].reduce((sum, value) => sum + value, 0)
  const geographyRoles = (strategy?.details.channelRecommendations ?? [])
    .filter(item => item.geographyRole)
    .slice(0, 3)
  return <section className="connected-strategy-support-card connected-strategy-geography"><header>
    <span className="connected-strategy-icon"><Icon name="globe" /></span><div><h2>Geographic allocation</h2>
      <p>Persisted market budgets across the current media mix.</p></div></header>
    <div className="connected-strategy-geography-body"><div className="connected-strategy-geo-ring"><div>
      <strong>{formatMoney(allocated || mix.totalBudgetMinor, mix.currency, 0)}</strong>
      <small>{allocated ? 'Allocated by market' : 'Total budget'}</small></div></div>
      <div className="connected-strategy-market-list">{markets.length ? markets.map(market => {
        const budget = marketBudgets.get(market) ?? 0
        const percent = mix.totalBudgetMinor > 0 ? budget / mix.totalBudgetMinor * 100 : 0
        return <div key={market}><span><i />{market}</span><strong>{budget > 0 ? `${round1(percent)}%` : '—'}</strong>
          <small>{budget > 0 ? formatMoney(budget, mix.currency, 0) : 'Not allocated yet'}</small></div>
      }) : <p>No campaign geography is retained.</p>}</div></div>
    {allocated === 0 && <p className="connected-strategy-context-note">
      Set the geography split in the media table. Advertified will not invent a per-market budget.</p>}
    {geographyRoles.length > 0 && <div className="connected-strategy-geography-roles">{geographyRoles.map(item =>
      <p key={item.channel}><strong>{mediaVisual(item.channel).label} guidance:</strong> {item.geographyRole}</p>)}</div>}
  </section>
}

function StrategySidebar(props: Props & { mix: MediaMix; strategy: MediaStrategyRecord | null }) {
  const { workspace, mix, strategy } = props
  const explanation = strategy?.details.summary || strategyExplanation(workspace, mix)
  const principles = strategy?.details.strategicPrinciples.length
    ? strategy.details.strategicPrinciples.slice(0, 5)
    : mix.allocations.map(item => item.role).filter(Boolean).slice(0, 5)
  return <aside className="connected-strategy-side connected-strategy-reference-side">
    <article className="connected-strategy-why"><header><span className="connected-ai-orb">✦</span><div>
      <h2>Why this strategy?</h2><p>{explanation}</p></div></header>
      <ul>{principles.map(value => <li key={value}><span>✓</span>{value}</li>)}</ul>
      <div className="connected-strategy-scope"><strong>Campaign scope</strong><span>{campaignModeLabel(workspace)}</span></div>
    </article>
    <ExpectedImpact {...props} />
    <article className="connected-strategy-image" aria-label="South African campaign context">
      <span>“Strategic media.<br />Stronger brands.<br />A more connected South Africa.”</span><i />
    </article>
  </aside>
}

type ImpactPresentation = {
  reachLabel: string
  frequencyLabel: string
  roiLabel: string
  statusLabel: string
  message: string
  source: string | null
  period: string | null
  methodology: string | null
}

function impactPresentation(workspace: PlanningWorkspace, mix: MediaMix): ImpactPresentation {
  const forecast = currentAudienceForecast(workspace)
  const estimate = mix.impactEstimate
  const inventoryBacked = hasInventoryForecast(forecast)
  const metrics = impactMetricValues(forecast, estimate)
  const provenance = impactProvenanceValues(forecast, estimate)
  return {
    ...impactMetricLabels(metrics),
    statusLabel: impactStatusLabel(inventoryBacked, Boolean(estimate)),
    message: impactMessage(inventoryBacked, Boolean(estimate)),
    ...provenance,
  }
}

function impactMetricValues(
  forecast: ReturnType<typeof currentAudienceForecast>,
  estimate: MediaImpactEstimate | null,
) {
  if (forecast && hasInventoryForecast(forecast)) {
    return {
      reach: forecast.deduplicatedReach,
      frequency: forecast.averageFrequency,
      roi: estimate ? estimate.estimatedRoiPercent : null,
    }
  }
  return {
    reach: estimate ? estimate.estimatedReach : null,
    frequency: estimate ? estimate.averageFrequency : null,
    roi: estimate ? estimate.estimatedRoiPercent : null,
  }
}

function impactMetricLabels(values: { reach: number | null; frequency: number | null; roi: number | null }) {
  return {
    reachLabel: values.reach === null ? 'Set estimate' : formatCompactNumber(values.reach),
    frequencyLabel: values.frequency === null ? 'Set estimate' : `${round1(values.frequency)}x`,
    roiLabel: values.roi === null ? 'Optional' : `${round1(values.roi)}%`,
  }
}

function impactProvenanceValues(
  forecast: ReturnType<typeof currentAudienceForecast>,
  estimate: MediaImpactEstimate | null,
) {
  if (forecast && hasInventoryForecast(forecast)) {
    return {
      source: forecast.measurementSource,
      period: forecast.measurementPeriod,
      methodology: forecast.methodology,
    }
  }
  if (!estimate) return { source: null, period: null, methodology: null }
  return { source: estimate.source, period: estimate.measurementPeriod, methodology: estimate.methodology }
}

function hasInventoryForecast(forecast: ReturnType<typeof currentAudienceForecast>) {
  return Boolean(forecast?.deduplicatedReach || forecast?.averageFrequency)
}

function impactStatusLabel(inventoryBacked: boolean, hasEstimate: boolean) {
  if (inventoryBacked) return 'Inventory-backed forecast'
  if (hasEstimate) return 'Pre-buy planning estimate'
  return 'Forecast not set'
}

function impactMessage(inventoryBacked: boolean, hasEstimate: boolean) {
  if (inventoryBacked) {
    return 'Reach and frequency use the retained inventory research basis for the current shortlist scenario. ROI remains a separate planning estimate unless an attributable value model is supplied.'
  }
  if (hasEstimate) {
    return 'These are explicit pre-buy planning estimates, not verified delivery. Source and methodology are retained with the strategy.'
  }
  return 'No inventory-backed forecast exists yet. Add a sourced pre-buy planning estimate now, or let reach and frequency populate from compatible inventory research after shortlist selection.'
}

function ExpectedImpact(props: Props & { mix: MediaMix; strategy: MediaStrategyRecord | null }) {
  const [editing, setEditing] = useState(false)
  const view = impactPresentation(props.workspace, props.mix)
  const editable = props.mix.status === masterDataCodes.lifecycleStatuses.draft
  async function saveImpact(value: MediaImpactEstimate | null) {
    await props.act(() => planningApi.updateMix(
      props.tenantId, props.mix, props.mix.allocations, props.token, value))
    setEditing(false)
  }
  return <article className="connected-strategy-impact">
    <ImpactHeader view={view} editable={editable} editing={editing} hasEstimate={Boolean(props.mix.impactEstimate)}
      onToggle={() => setEditing(value => !value)} />
    <ImpactMetrics view={view} />
    <p>{view.message}</p>
    <ImpactProvenance view={view} />
    {editing && <ImpactEstimateEditor initial={props.mix.impactEstimate} busy={props.busy}
      onCancel={() => setEditing(false)} onSave={saveImpact} />}
    <ImpactSuccessMeasures values={props.workspace.decisionContext?.successMeasures ?? []} />
  </article>
}

function ImpactHeader({ view, editable, editing, hasEstimate, onToggle }: {
  view: ImpactPresentation
  editable: boolean
  editing: boolean
  hasEstimate: boolean
  onToggle: () => void
}) {
  return <header><Icon name="chart" /><div><h2>Expected impact</h2><span>{view.statusLabel}</span></div>
    {editable && <button className="text-action" type="button" onClick={onToggle}>
      {impactEditLabel(editing, hasEstimate)}</button>}</header>
}

function impactEditLabel(editing: boolean, hasEstimate: boolean) {
  if (editing) return 'Cancel'
  return hasEstimate ? 'Edit estimate' : 'Add planning estimate'
}

function ImpactMetrics({ view }: { view: ImpactPresentation }) {
  return <div className="connected-strategy-impact-metrics">
    <div><Icon name="users" /><strong>{view.reachLabel}</strong><span>Estimated reach</span></div>
    <div><Icon name="target" /><strong>{view.frequencyLabel}</strong><span>Average frequency</span></div>
    <div><Icon name="chart" /><strong>{view.roiLabel}</strong><span>Estimated ROI</span></div>
  </div>
}

function ImpactProvenance({ view }: { view: ImpactPresentation }) {
  if (!view.source && !view.methodology && !view.period) return null
  return <div className="connected-strategy-impact-provenance">
    {view.source && <span><strong>Source</strong>{view.source}</span>}
    {view.period && <span><strong>Period</strong>{view.period}</span>}
    {view.methodology && <span><strong>Method</strong>{view.methodology}</span>}
  </div>
}

function ImpactSuccessMeasures({ values }: { values: string[] }) {
  if (!values.length) return null
  return <div className="connected-strategy-success-measures"><strong>Success measures</strong>
    {values.slice(0, 4).map(value => <span key={value}>• {value}</span>)}</div>
}

type ImpactDraft = {
  reach: string
  frequency: string
  roi: string
  source: string
  period: string
  methodology: string
}

function ImpactEstimateEditor({ initial, busy, onCancel, onSave }: {
  initial: MediaImpactEstimate | null
  busy: boolean
  onCancel: () => void
  onSave: (value: MediaImpactEstimate | null) => Promise<void>
}) {
  const [draft, setDraft] = useState<ImpactDraft>(() => impactDraft(initial))
  const estimate = parseImpactDraft(draft)
  async function save() {
    if (estimate) await onSave(estimate)
  }
  return <section className="connected-strategy-impact-editor" aria-label="Planning impact estimate">
    <div className="connected-strategy-impact-editor-grid">
      <ImpactNumberField label="Estimated reach" value={draft.reach} min="1" step="1" placeholder="e.g. 2400000"
        onChange={value => setDraft(current => ({ ...current, reach: value }))} />
      <ImpactNumberField label="Average frequency" value={draft.frequency} min="0.1" step="0.1" placeholder="e.g. 3.2"
        onChange={value => setDraft(current => ({ ...current, frequency: value }))} />
      <ImpactNumberField label="Estimated ROI %" value={draft.roi} min="-100" step="0.1" placeholder="e.g. 150" optional
        onChange={value => setDraft(current => ({ ...current, roi: value }))} />
    </div>
    <ImpactTextField label="Source" value={draft.source} placeholder="Research provider, client model or planner assumption"
      onChange={value => setDraft(current => ({ ...current, source: value }))} />
    <ImpactTextField label="Measurement period" value={draft.period} placeholder="e.g. 2026 Q4" optional
      onChange={value => setDraft(current => ({ ...current, period: value }))} />
    <label>Methodology<textarea value={draft.methodology}
      onChange={event => setDraft(current => ({ ...current, methodology: event.target.value }))}
      placeholder="Explain how the estimate was produced and its limitations." /></label>
    <ImpactEditorFooter initial={initial} busy={busy} valid={Boolean(estimate)} onCancel={onCancel}
      onClear={() => onSave(null)} onSave={save} />
  </section>
}

function ImpactNumberField({ label, value, min, step, placeholder, optional, onChange }: {
  label: string; value: string; min: string; step: string; placeholder: string; optional?: boolean
  onChange: (value: string) => void
}) {
  return <label>{label}{optional && <span>optional</span>}<input type="number" min={min} step={step}
    value={value} onChange={event => onChange(event.target.value)} placeholder={placeholder} /></label>
}

function ImpactTextField({ label, value, placeholder, optional, onChange }: {
  label: string; value: string; placeholder: string; optional?: boolean; onChange: (value: string) => void
}) {
  return <label>{label}{optional && <span>optional</span>}<input value={value}
    onChange={event => onChange(event.target.value)} placeholder={placeholder} /></label>
}

function ImpactEditorFooter({ initial, busy, valid, onCancel, onClear, onSave }: {
  initial: MediaImpactEstimate | null; busy: boolean; valid: boolean
  onCancel: () => void; onClear: () => Promise<void>; onSave: () => Promise<void>
}) {
  return <footer><button className="secondary-button" type="button" disabled={busy} onClick={onCancel}>Cancel</button>
    {initial && <button className="secondary-button" type="button" disabled={busy}
      onClick={() => void onClear()}>Clear estimate</button>}
    <button className="primary-button" type="button" disabled={busy || !valid} onClick={() => void onSave()}>
      {busy ? 'Saving estimate…' : 'Save planning estimate'}</button></footer>
}

function impactDraft(initial: MediaImpactEstimate | null): ImpactDraft {
  if (!initial) return { reach: '', frequency: '', roi: '', source: '', period: '', methodology: '' }
  return {
    reach: initial.estimatedReach?.toString() ?? '',
    frequency: initial.averageFrequency?.toString() ?? '',
    roi: initial.estimatedRoiPercent?.toString() ?? '',
    source: initial.source,
    period: initial.measurementPeriod ?? '',
    methodology: initial.methodology,
  }
}

function parseImpactDraft(draft: ImpactDraft): MediaImpactEstimate | null {
  const reach = positiveNumber(draft.reach)
  const frequency = positiveNumber(draft.frequency)
  const roi = numericValue(draft.roi)
  if (reach === null && frequency === null && roi === null) return null
  if (roi !== null && roi < -100) return null
  const source = draft.source.trim()
  const methodology = draft.methodology.trim()
  if (!source || !methodology) return null
  return {
    estimatedReach: reach,
    averageFrequency: frequency,
    estimatedRoiPercent: roi,
    source,
    measurementPeriod: draft.period.trim() || null,
    methodology,
  }
}

function StrategyFooter(props: Props & { mix: MediaMix }) {
  const nextLabel = props.workspace.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly
    ? 'Next: Inventory' : 'Next: Media Plan'
  const ready = strategyFlightsReady(props.mix) && strategyGeographiesReady(props.mix, campaignMarkets(props.workspace))
  return <footer className="connected-strategy-page-footer"><Link className="secondary-button" to={`/stp/${props.workspace.briefVersionId}`}>
    ← Back to Audience &amp; STP</Link>
    {isApproved(props.mix.status)
      ? <Link className="primary-button" to={`/planning/${props.workspace.briefVersionId}`}>{nextLabel} →</Link>
      : <button className="primary-button" type="button" disabled={props.busy || !ready}
        onClick={() => void props.act(() => planningApi.approveMix(props.tenantId, props.mix, props.token))}>
        {props.busy ? 'Confirming strategy…' : 'Approve strategy & continue'}</button>}
  </footer>
}

function strategyFlightsReady(mix: MediaMix) {
  return mix.allocations.length > 0 && mix.allocations.every(item => item.runningPeriods.length > 0
    && item.runningPeriods.every(period => period.start && period.end && period.start <= period.end))
}

function allocationPercentMap(mix: MediaMix) {
  return Object.fromEntries(mix.allocations.map(item => [item.channel, allocationPercentNumber(item, mix)]))
}

function allocationPercentNumber(item: MediaAllocation, mix: MediaMix) {
  if (!mix.totalBudgetMinor) return 0
  return item.budgetMinor / mix.totalBudgetMinor * 100
}

function allocationsFromPercentages(mix: MediaMix, values: Record<string, number>): MediaAllocation[] {
  let assigned = 0
  return mix.allocations.map((item, index) => {
    const isLast = index === mix.allocations.length - 1
    const budgetMinor = isLast
      ? Math.max(0, mix.totalBudgetMinor - assigned)
      : Math.round(mix.totalBudgetMinor * (values[item.channel] ?? 0) / 100)
    assigned += budgetMinor
    return { ...item, budgetMinor, geographyAllocations: scaleGeographyAllocations(item, budgetMinor) }
  })
}

function totalPercent(values: Record<string, number>) {
  return Object.values(values).reduce((sum, value) => sum + (Number.isFinite(value) ? value : 0), 0)
}

function clampPercent(value: number) {
  if (!Number.isFinite(value)) return 0
  return Math.max(0, Math.min(100, value))
}

function round1(value: number) {
  return Math.round(value * 10) / 10
}

function campaignMarkets(workspace: PlanningWorkspace) {
  const targets = new Set(workspace.audience?.targetAudienceIds ?? [])
  return [...new Set((workspace.audience?.definitions ?? [])
    .filter(item => targets.size === 0 || targets.has(item.id))
    .flatMap(item => item.geographies)
    .map(value => value.trim())
    .filter(Boolean))]
}

function strategyGeographiesReady(mix: MediaMix, markets: string[]) {
  if (markets.length === 0) return true
  const allowed = new Set(markets.map(value => value.toLocaleLowerCase()))
  return mix.allocations.filter(item => item.budgetMinor > 0).every(item => {
    if (!item.geographyAllocations.length) return false
    if (item.geographyAllocations.some(value => !allowed.has(value.geography.toLocaleLowerCase()))) return false
    return item.geographyAllocations.reduce((sum, value) => sum + value.budgetMinor, 0) === item.budgetMinor
  })
}

function aggregateGeographyBudgets(mix: MediaMix) {
  const result = new Map<string, number>()
  for (const allocation of mix.allocations) {
    for (const geography of allocation.geographyAllocations) {
      result.set(geography.geography, (result.get(geography.geography) ?? 0) + geography.budgetMinor)
    }
  }
  return result
}

function splitBudgetEvenly(total: number, markets: string[]): MediaGeographyAllocation[] {
  if (markets.length === 0) return []
  const base = Math.floor(total / markets.length)
  let assigned = 0
  return markets.map((geography, index) => {
    const budgetMinor = index === markets.length - 1 ? total - assigned : base
    assigned += budgetMinor
    return { geography, budgetMinor }
  })
}

function scaleGeographyAllocations(item: MediaAllocation, budgetMinor: number): MediaGeographyAllocation[] {
  if (!item.geographyAllocations.length) return []
  if (item.geographyAllocations.length === 1) {
    return [{ geography: item.geographyAllocations[0].geography, budgetMinor }]
  }
  const sourceTotal = item.geographyAllocations.reduce((sum, value) => sum + value.budgetMinor, 0)
  if (sourceTotal <= 0) return []
  let assigned = 0
  return item.geographyAllocations.map((value, index) => {
    const next = index === item.geographyAllocations.length - 1
      ? budgetMinor - assigned
      : Math.round(budgetMinor * value.budgetMinor / sourceTotal)
    assigned += next
    return { geography: value.geography, budgetMinor: Math.max(0, next) }
  })
}

function currentAudienceForecast(workspace: PlanningWorkspace) {
  const combinations = workspace.shortlist?.campaignCombinations
  if (!combinations) return null
  const alternatives = combinations.alternatives
  if (!alternatives.length) return null
  const selectedIds = selectedShortlistIds(workspace)
  const exact = alternatives.find(item => sameCandidateSet(item.candidateIds, selectedIds))
  const chosen = exact ?? alternatives.find(item => item.scenario?.recommended)
  return chosen?.audienceForecast ?? null
}

function selectedShortlistIds(workspace: PlanningWorkspace) {
  return (workspace.shortlist?.candidates ?? []).filter(item => item.isSelected).map(item => item.id)
}

function sameCandidateSet(candidateIds: string[], selectedIds: string[]) {
  if (!selectedIds.length || candidateIds.length !== selectedIds.length) return false
  const selected = new Set(selectedIds)
  return candidateIds.every(id => selected.has(id))
}

function formatCompactNumber(value: number) {
  if (value >= 1_000_000) return `${round1(value / 1_000_000)}M`
  if (value >= 1_000) return `${round1(value / 1_000)}K`
  return Math.round(value).toLocaleString('en-ZA')
}

function positiveNumber(value: string) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null
}

function numericValue(value: string) {
  if (!value.trim()) return null
  const parsed = Number(value)
  return Number.isFinite(parsed) ? parsed : null
}

type TimelineRow = { channel: string; start: string; end: string }

function timelineRows(mix: MediaMix): TimelineRow[] {
  return mix.allocations.flatMap(item => {
    const periods = item.runningPeriods.filter(period => validDate(period.start) && validDate(period.end))
    if (!periods.length) return []
    return [{
      channel: item.channel,
      start: periods.map(period => period.start).sort()[0],
      end: periods.map(period => period.end).sort().at(-1)!,
    }]
  })
}

function timelineBounds(rows: TimelineRow[]) {
  if (!rows.length) return null
  return {
    start: rows.map(row => row.start).sort()[0],
    end: rows.map(row => row.end).sort().at(-1)!,
  }
}

function timelineMonths(start: string, end: string) {
  const result: string[] = []
  const first = new Date(`${start}T00:00:00Z`)
  const last = new Date(`${end}T00:00:00Z`)
  const cursor = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), 1))
  while (cursor <= last && result.length < 6) {
    result.push(cursor.toLocaleDateString('en-ZA', { month: 'short', year: 'numeric', timeZone: 'UTC' }))
    cursor.setUTCMonth(cursor.getUTCMonth() + 1)
  }
  return result
}

function timelineOffset(value: string, start: string, end: string) {
  const total = Math.max(1, dayDistance(start, end) + 1)
  return Math.max(0, dayDistance(start, value) / total * 100)
}

function timelineWidth(rowStart: string, rowEnd: string, start: string, end: string) {
  const total = Math.max(1, dayDistance(start, end) + 1)
  return Math.max(2, (dayDistance(rowStart, rowEnd) + 1) / total * 100)
}

function dayDistance(start: string, end: string) {
  const from = Date.parse(`${start}T00:00:00Z`)
  const to = Date.parse(`${end}T00:00:00Z`)
  return Math.round((to - from) / 86_400_000)
}

function validDate(value: string) {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(`${value}T00:00:00Z`))
}
