import type { MediaMix, MediaPlan, Shortlist } from '../api/planning-schemas'
import { formatMoney, formatNumber, humanizeCode } from '../presentation/format'
import { mediaVisual } from './media-visuals'

type Props = { mix: MediaMix; shortlist: Shortlist | null; plan: MediaPlan | null }
type Forecast = NonNullable<NonNullable<Shortlist['campaignCombinations']>['alternatives'][number]['audienceForecast']>

export function ApprovedPlanningOverview({ mix, shortlist, plan }: Props) {
  const total = Math.max(mix.totalBudgetMinor, 1)
  const selectedLines = plan?.lines ?? []
  const forecast = recommendedForecast(shortlist)
  return <section className="approved-planning-overview" aria-labelledby="approved-planning-overview-title">
    <header><div><p className="eyebrow">Media Planning Overview</p>
      <h2 id="approved-planning-overview-title">Integrated plan across selected channels</h2></div>
      <span>{humanizeCode(mix.status, true)}</span></header>
    <PlanningKpis mix={mix} forecast={forecast} />
    <div className="approved-planning-visual-grid">
      <InvestmentVisual mix={mix} total={total} />
      <MediaFlight mix={mix} />
    </div>
    <TopPlacements lines={selectedLines} currency={plan?.currency ?? mix.currency} />
  </section>
}

function recommendedForecast(shortlist: Shortlist | null): Forecast | null {
  const alternatives = shortlist?.campaignCombinations?.alternatives ?? []
  const recommended = alternatives.find(item => item.scenario?.recommended) ?? alternatives[0]
  return recommended?.audienceForecast ?? null
}

function PlanningKpis({ mix, forecast }: { mix: MediaMix; forecast: Forecast | null }) {
  const reach = metric(forecast?.deduplicatedReach, 0,
    'Measured deduplicated campaign reach', 'Requires compatible overlap evidence')
  const frequency = metric(forecast?.averageFrequency, 2,
    'Measured impressions ÷ deduplicated reach', 'Requires compatible deduplicated reach')
  const impressions = metric(forecast?.totalImpressions, 0,
    'Compatible measured placement impressions', 'Requires compatible delivery evidence')
  return <div className="approved-planning-kpis">
    <PlanKpi label="Total Investment" value={formatMoney(mix.totalBudgetMinor, mix.currency, 0)} note="Planning budget" />
    <PlanKpi label="Total Reach" {...reach} />
    <PlanKpi label="Avg. Frequency" {...frequency} />
    <PlanKpi label="Impressions" {...impressions} />
  </div>
}

function metric(value: number | null | undefined, digits: number, known: string, unknown: string) {
  return value === null || value === undefined
    ? { value: '—', note: unknown }
    : { value: formatNumber(value, digits), note: known }
}

function PlanKpi({ label, value, note }: { label: string; value: string; note: string }) {
  return <article><span>{label}</span><strong>{value}</strong><small>{note}</small></article>
}

function InvestmentVisual({ mix, total }: { mix: MediaMix; total: number }) {
  const gradient = mix.allocations.map((item, index) => {
    const previous = mix.allocations.slice(0, index).reduce((sum, value) => sum + value.budgetMinor, 0)
    const start = previous / total * 100
    const end = (previous + item.budgetMinor) / total * 100
    return `${mediaVisual(item.channel).color} ${start}% ${end}%`
  }).join(', ')
  return <article className="approved-planning-investment"><header><h3>Investment by Channel</h3></header>
    <div><div className="approved-plan-donut" style={{ background: `conic-gradient(${gradient || '#edf0f4 0 100%'})` }}>
      <span><strong>{formatMoney(mix.totalBudgetMinor, mix.currency, 0)}</strong><small>Total</small></span></div>
      <div className="approved-plan-legend">{mix.allocations.map(item => <AllocationLegend key={item.channel}
        channel={item.channel} budget={item.budgetMinor} total={total} currency={mix.currency} />)}</div></div></article>
}

function AllocationLegend({ channel, budget, total, currency }: {
  channel: string; budget: number; total: number; currency: string
}) {
  const visual = mediaVisual(channel)
  return <div><i style={{ background: visual.color }} /><strong>{visual.label}</strong>
    <span>{Math.round(budget / total * 100)}%</span><small>{formatMoney(budget, currency, 0)}</small></div>
}

function MediaFlight({ mix }: { mix: MediaMix }) {
  return <article className="approved-media-flight"><header><h3>Media Flight</h3></header><div>
    {mix.allocations.map((item, index) => <FlightRow key={item.channel} item={item} index={index} />)}
  </div></article>
}

function FlightRow({ item, index }: { item: MediaMix['allocations'][number]; index: number }) {
  const period = item.runningPeriods[0]
  const visual = mediaVisual(item.channel)
  return <div><strong>{visual.label}</strong><span><i
    style={{ width: `${Math.max(18, 88 - index * 9)}%`, background: visual.color }} /></span>
    <small>{period ? `${period.start} → ${period.end}` : 'Dates not supplied'}</small></div>
}

function TopPlacements({ lines, currency }: { lines: MediaPlan['lines']; currency: string }) {
  return <article className="approved-top-placements"><header><h3>Top Placements</h3>
    <span>{lines.length} planned line{lines.length === 1 ? '' : 's'}</span></header>
    {lines.length === 0 ? <p className="approved-empty">Top placements will appear after inventory is selected and the plan is created.</p>
      : <div className="approved-placement-table"><div><span>Channel</span><span>Placement</span><span>Location</span>
        <span>Flight</span><span>Investment</span></div>
        {lines.slice(0, 7).map(line => <PlacementRow key={line.id} line={line} currency={currency} />)}</div>}
  </article>
}

function PlacementRow({ line, currency }: { line: MediaPlan['lines'][number]; currency: string }) {
  const period = line.runningPeriods[0]
  return <div><strong>{humanizeCode(line.channel, true)}</strong><span>{line.name}</span><span>{line.geography}</span>
    <span>{period ? `${period.start} – ${period.end}` : '—'}</span>
    <strong>{formatMoney(line.clientPriceMinor, currency, 0)}</strong></div>
}
