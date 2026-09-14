import type { AudienceResearchContext, AudienceResearchObservation, AudienceStrategy } from '../api/planning-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'

type Segment = AudienceStrategy['definitions'][number]
type Distribution = Array<[string, number]>

const DEFAULT_CHANNELS = ['OOH', 'RADIO', 'DIGITAL', 'SOCIAL', 'TV'] as const
const PLACE_CHANNELS = new Set(['OOH', 'DOOH', 'RETAIL', 'TRANSIT', 'MALL'])
const DIGITAL_CHANNELS = new Set(['DIGITAL', 'SOCIAL', 'MOBILE', 'EMAIL'])
const DIGITAL_ACCESS_SEGMENTS = ['Any kind of access', 'Mobile', 'Fixed Internet at home', 'Public Wi-Fi'] as const

export function GeographicConcentration({ audience, research }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
}) {
  const rows = geographyRows(audience)
  const total = rows.reduce((sum, [, count]) => sum + count, 0)
  const expanded = expandedResearchGeographies(research)
  return <section className="connected-geography-card"><header><div><span><Icon name="globe" /></span>
    <strong>Geographic Concentration</strong></div><em>Brief signal</em></header>
    <div className="connected-geography-layout">
      {rows.length ? <div className="connected-geography-bars">{rows.map(([name, count]) => {
        const share = Math.round(count / Math.max(total, 1) * 100)
        return <div key={name} title="Share of retained audience geography mentions; not a population estimate">
          <span>{displayLabel(name)}</span><i><b style={{ width: `${Math.max(10, share)}%` }} /></i>
          <strong>{share}%</strong></div>
      })}</div> : <p className="connected-audience-unavailable">No governed audience geography is retained yet.</p>}
      <AudienceSouthAfricaMap rows={rows} />
    </div>
    <small className="connected-geography-note">Percentages show the mix of retained Brief geography signals, not population concentration.
      {expanded.length > 0 && <> Research context also resolves to {expanded.join(', ')}.</>}</small>
  </section>
}

export function AudienceEvidenceDashboard({ audience, research }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
}) {
  const ageValues = researchAgeDistribution(research)
  const lsmSemValues = countValues(audience.definitions.map(item => item.lsmSem))
  const socioEconomicValues = socioEconomicDistribution(research)
  return <div className="connected-audience-evidence-grid">
    <EvidenceCard title="Age Distribution" values={ageValues.length ? ageValues : ageBandValues(audience)}
      empty="Age distribution requires approved audience research." previewLabels={['15–24', '25–34', '35–49', '50–64', '65+']}
      suffix={ageValues.length ? '%' : ''} contextLabel={ageValues.length ? '15+ market context' : undefined} />
    <GenderEvidenceCard research={research} />
    <EvidenceCard title={lsmSemValues.length ? 'LSM / SEM Group' : 'Socio-economic Context'}
      values={lsmSemValues.length ? lsmSemValues : socioEconomicValues}
      empty="No licensed LSM / SEM taxonomy or governed socio-economic context is available yet."
      previewLabels={['Gauteng', 'Mpumalanga', 'Limpopo']}
      suffix={lsmSemValues.length ? '' : '%'}
      contextLabel={lsmSemValues.length ? 'Governed segment evidence' : socioEconomicValues.length ? 'Labour-force context' : undefined} />
    <TopPlacesCard audience={audience} />
    <PersonaCard audience={audience} />
    <DigitalAccessCard research={research} />
    <ResearchProvenance research={research} />
  </div>
}

export function AudienceRelevanceMatrix({ audience, research, channels = [] }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
  channels?: readonly string[]
}) {
  const visibleChannels = [...new Set((channels.length ? channels : DEFAULT_CHANNELS)
    .map(normalizeChannel).filter(Boolean))].slice(0, 6)
  return <section className="connected-audience-relevance"><header><div><Icon name="target" />
    <div><h2>Channel Planning Signal Matrix</h2>
      <p>Shows usable planning signals now — Brief geography, governed market-access context and explicit research gaps — without inventing media affinity.</p></div></div>
    <span className="connected-relevance-legend"><i className="is-signal" /> Planning signal <i /> Research gap</span></header>
    <div className="connected-relevance-table" role="table" aria-label="Audience channel planning signals">
      <div className="connected-relevance-row is-head" role="row"
        style={{ gridTemplateColumns: relevanceColumns(visibleChannels.length) }}>
        <span>Audience Segment</span><span>Evidence</span>{visibleChannels.map(channel => <span key={channel}>{channelLabel(channel)}</span>)}
      </div>
      {audience.definitions.map(item => <div className="connected-relevance-row" role="row" key={item.id}
        style={{ gridTemplateColumns: relevanceColumns(visibleChannels.length) }}>
        <strong>{displayLabel(item.name)}</strong><span>{confidenceLabel(item)}</span>
        {visibleChannels.map(channel => <ChannelPlanningSignal key={channel} channel={channel} item={item} research={research} />)}
      </div>)}
    </div>
  </section>
}

function ChannelPlanningSignal({ channel, item, research }: {
  channel: string
  item: Segment
  research: AudienceResearchContext | null
}) {
  const signal = channelSignal(channel, item, research)
  return <span className={`connected-relevance-signal is-${signal.kind}`} title={signal.detail}>
    <b>{signal.label}</b><small>{signal.source}</small>
  </span>
}

function channelSignal(channel: string, item: Segment, research: AudienceResearchContext | null) {
  if (PLACE_CHANNELS.has(channel) && item.geographies.length > 0) return {
    kind: 'brief', label: 'Geo-ready', source: 'Brief geography',
    detail: 'The audience has approved campaign geographies, so place-based planning can proceed. This is not measured channel affinity.',
  }
  if (DIGITAL_CHANNELS.has(channel)) {
    const context = digitalMarketAccess(research)
    if (context) return {
      kind: 'context', label: `${Math.round(context.average)}% access`, source: `${context.markets} market${context.markets === 1 ? '' : 's'}`,
      detail: 'Governed Stats SA household internet-access context for campaign markets. This supports digital feasibility, not audience-specific preference.',
    }
  }
  if (item.referenceObservationIds.length > 0) return {
    kind: 'context', label: 'Evidence held', source: 'Segment support',
    detail: 'This audience carries governed supporting observations, but no retained observation establishes channel-specific media use.',
  }
  return {
    kind: 'gap', label: 'Needs evidence', source: 'Media use',
    detail: 'No governed audience-specific media-use, reach, daypart or channel-affinity evidence is retained for this audience and channel.',
  }
}

function digitalMarketAccess(research: AudienceResearchContext | null) {
  const observations = (research?.observations ?? []).filter(item =>
    item.dimensions.group === 'HOUSEHOLD_INTERNET_ACCESS' && item.metricCode === 'SHARE_PERCENT' &&
    item.geographyLevel !== 'COUNTRY' && item.dimensions.segment === 'Any kind of access')
  const rows = observations.length ? observations : (research?.observations ?? []).filter(item =>
    item.dimensions.group === 'HOUSEHOLD_INTERNET_ACCESS' && item.metricCode === 'SHARE_PERCENT' &&
    item.geographyLevel !== 'COUNTRY' && item.dimensions.segment === 'Mobile')
  if (!rows.length) return null
  const byMarket = new Map<string, number>()
  rows.forEach(item => { if (!byMarket.has(item.geographyName)) byMarket.set(item.geographyName, item.metricValue) })
  const values = [...byMarket.values()]
  return { average: values.reduce((sum, value) => sum + value, 0) / values.length, markets: values.length }
}

function normalizeChannel(value: string) {
  return value.trim().toUpperCase().replace(/[^A-Z0-9]+/g, '_').replace(/^_|_$/g, '')
}

function channelLabel(value: string) {
  const labels: Record<string, string> = {
    OOH: 'OOH', DOOH: 'DOOH', RADIO: 'Radio', TV: 'TV', DIGITAL: 'Digital', SOCIAL: 'Social',
    INFLUENCER: 'Influencers', MOBILE: 'Mobile', EMAIL: 'Email', RETAIL: 'Retail', TRANSIT: 'Transit', MALL: 'Mall',
  }
  return labels[value] ?? value.replaceAll('_', ' ')
}

function relevanceColumns(channelCount: number) {
  return `1.7fr 1.1fr repeat(${Math.max(channelCount, 1)}, minmax(105px, .9fr))`
}

export function AudienceInsights({ audience, research, rationale, positioning }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
  rationale: string
  positioning: string
}) {
  const insights = compactInsights(audience, research, rationale, positioning)
  return <aside className="connected-audience-insights"><header><span className="connected-ai-orb">✦</span><div>
    <h2>AI Audience Insights</h2><p>Planner-ready observations separated from evidence gaps.</p></div></header>
    <ul>{insights.map((insight, index) => <li key={`${insight}-${index}`}><span>✓</span>{insight}</li>)}</ul>
  </aside>
}

function EvidenceCard({ title, values = [], empty, previewLabels = [], suffix = '', contextLabel }: {
  title: string
  values?: Distribution
  empty: string
  previewLabels?: string[]
  suffix?: string
  contextLabel?: string
}) {
  const max = Math.max(...values.map(([, count]) => count), 1)
  return <section className="connected-audience-metric-card"><header><strong>{title}</strong>
    {values.length ? contextLabel && <span className="connected-context-chip">{contextLabel}</span>
      : <span className="connected-research-chip">Research required</span>}</header>
    {values.length ? <div className="connected-metric-columns">{values.slice(0, 5).map(([label, count]) => <div key={label}>
      <small>{formatMetric(count, suffix)}</small><i><b style={{ height: `${Math.max(18, count / max * 100)}%` }} /></i><span>{label}</span>
    </div>)}</div> : <EmptyMetricPreview labels={previewLabels} message={empty} />}
  </section>
}

function GenderEvidenceCard({ research }: { research: AudienceResearchContext | null }) {
  const values = researchShareDistribution(research, ['SEX'])
  if (!values.length) return <section className="connected-audience-metric-card connected-gender-card"><header><strong>Gender</strong>
    <span className="connected-research-chip">Research required</span></header>
    <div className="connected-gender-preview" aria-label="Gender distribution not established"><div><span>?</span></div>
      <p>Gender split is not present in governed audience evidence.</p></div>
  </section>
  const female = values.find(([label]) => label.toLowerCase() === 'female')?.[1] ?? values[0]?.[1] ?? 0
  return <section className="connected-audience-metric-card connected-gender-card"><header><strong>Gender</strong>
    <span className="connected-context-chip">SA market context</span></header>
    <div className="connected-gender-research"><div className="connected-gender-donut"
      style={{ background: `conic-gradient(#6038f5 0 ${female}%, #dcd6ff ${female}% 100%)` }}>
      <span>{formatMetric(female, '%')}</span></div>
      <div>{values.slice(0, 3).map(([label, value]) => <p key={label}><span>{label}</span><strong>{formatMetric(value, '%')}</strong></p>)}</div>
    </div>
    <small className="connected-metric-caveat">National population context; not the gender split of these campaign audiences.</small>
  </section>
}

function EmptyMetricPreview({ labels, message }: { labels: string[]; message: string }) {
  return <div className="connected-empty-metric" title={message}><div>
    {labels.map((label, index) => <span key={label}><i style={{ height: `${28 + (index % 3) * 13}%` }} /><small>{label}</small></span>)}
  </div><p>{message}</p></div>
}

function TopPlacesCard({ audience }: { audience: AudienceStrategy }) {
  const rows = geographyRows(audience)
  const max = Math.max(...rows.map(([, value]) => value), 1)
  return <section className="connected-audience-metric-card connected-top-places"><header><strong>Top Places</strong>
    <span>{rows.length ? `Top ${Math.min(rows.length, 8)}` : '—'}</span></header>
    {rows.length ? <div>{rows.map(([name, count]) => <div key={name}><span>{displayLabel(name)}</span>
      <i><b style={{ width: `${Math.max(14, count / max * 100)}%` }} /></i>
      <small>{count}</small></div>)}</div> : <p className="connected-audience-unavailable">No governed place ranking is retained.</p>}
  </section>
}

function PersonaCard({ audience }: { audience: AudienceStrategy }) {
  return <section className="connected-audience-metric-card connected-audience-personas"><header><strong>Audience Personas</strong>
    <span>{audience.definitions.length}</span></header><div>{audience.definitions.slice(0, 2).map(item =>
      <Persona key={item.id} item={item} />)}</div></section>
}

function Persona({ item }: { item: Segment }) {
  const chips = [item.lifeStage, item.lsmSem, ...item.geographies.map(displayLabel)].filter(Boolean).slice(0, 3)
  return <article><span className="connected-persona-avatar">{initials(item.name)}</span><div>
    <strong>{displayLabel(item.name)}</strong><small>{item.description}</small>
    <div className="connected-persona-chips">{chips.map(value => <em key={value}>{value}</em>)}</div>
  </div></article>
}

function DigitalAccessCard({ research }: { research: AudienceResearchContext | null }) {
  const rows = digitalAccessRows(research)
  return <section className="connected-audience-metric-card connected-channel-affinity"><header><strong>Digital Access Context</strong>
    {rows.length ? <span className="connected-context-chip">GHS context</span>
      : <span className="connected-research-chip">Research required</span>}</header>
    {rows.length ? <div>{rows.slice(0, 5).map(item => <div key={item.observationId}>
      <span title={`${item.geographyName}: ${item.dimensions.segment}`}>{item.geographyName}</span>
      <i><b style={{ width: `${Math.max(8, Math.min(item.metricValue, 100))}%` }} /></i>
      <small>{formatMetric(item.metricValue, '%')}</small></div>)}</div>
      : <p className="connected-audience-unavailable">No governed digital-access context is retained for these campaign markets.</p>}
    {rows.length > 0 && <small className="connected-metric-caveat">Household internet access is planning context, not media-channel affinity.</small>}
  </section>
}

function ResearchProvenance({ research }: { research: AudienceResearchContext | null }) {
  const observations = research?.observations ?? []
  if (!observations.length) return <div className="connected-research-provenance is-empty"><Icon name="evidence" />
    <span><strong>No governed market-context observations matched this Brief yet.</strong>
      <small>Audience-specific traits remain research gaps until a valid source is attached.</small></span></div>
  const sources = [...new Map(observations.map(item => [`${item.sourceTitle}|${item.measurementPeriod}`, item])).values()]
  const expanded = expandedResearchGeographies(research)
  return <div className="connected-research-provenance"><Icon name="evidence" /><span>
    <strong>{observations.length} governed market-context observations available</strong>
    <small>{sources.slice(0, 3).map(item => `${item.sourceTitle} (${item.measurementPeriod})`).join(' · ')}
      {expanded.length > 0 ? ` · Context scope: ${expanded.join(', ')}` : ''}</small></span></div>
}

function AudienceSouthAfricaMap({ rows }: { rows: Distribution }) {
  const names = rows.map(([name]) => name.toLowerCase())
  const pins = [
    { label: 'JHB', x: 68, y: 32, match: ['johannesburg', 'gauteng', 'sandton', 'pretoria'] },
    { label: 'DBN', x: 78, y: 62, match: ['durban', 'kwazulu', 'kwazulu-natal', 'kzn', 'umhlanga'] },
    { label: 'CPT', x: 24, y: 76, match: ['cape town', 'western cape'] },
  ].filter(pin => pin.match.some(term => names.some(name => name.includes(term))))
  return <div className="connected-mini-sa-map" aria-label="South Africa audience geography summary">
    <svg viewBox="0 0 100 90" role="img" aria-hidden="true"><path d="M10 42 20 28 34 22 47 18 62 20 76 17 90 29 94 45 88 62 77 73 62 78 48 76 37 82 24 74 18 61 9 54Z" />
      <path className="is-lesotho" d="m66 58 5-5 5 5-4 6Z" />
      {pins.map(pin => <g key={pin.label} transform={`translate(${pin.x} ${pin.y})`}><circle r="5" /><circle r="2" />
        <text x="6" y="3">{pin.label}</text></g>)}</svg>
    <small>{geographySummary(rows.length)}</small>
  </div>
}

function compactInsights(audience: AudienceStrategy, research: AudienceResearchContext | null, rationale: string, positioning: string) {
  const insights: string[] = []
  const researchSentence = researchCoverageSentence(research)
  if (researchSentence) insights.push(researchSentence)
  const digitalSentence = digitalAccessSentence(research)
  if (digitalSentence) insights.push(digitalSentence)
  if (rationale.trim()) insights.push(rationale.trim())
  if (positioning.trim()) insights.push(positioning.trim())
  insights.push(evidenceCoverageSentence(audience))
  if (insights.length < 5) insights.push(geographyCoverageSentence(audience))
  if (!hasMediaEvidence(audience) && insights.length < 5)
    insights.push('Media-use, daypart and channel-affinity evidence still needs audience-specific research before channel fit can be scored.')
  return insights.slice(0, 5)
}

function ageBandValues(audience: AudienceStrategy) {
  const values = audience.definitions.map(item => item.lifeStage)
    .filter((value): value is string => Boolean(value && /(?:\d{2}\s*[–-]\s*\d{2}|\d{2}\+)/.test(value)))
  return countValues(values)
}

function researchAgeDistribution(research: AudienceResearchContext | null): Distribution {
  const observations = (research?.observations ?? []).filter(item =>
    ['AGE', 'AGE_GROUP'].includes(item.dimensions.group ?? ''))
  if (!observations.length) return []
  const latestPeriod = observations.map(item => item.measurementPeriod).sort().at(-1)
  const latest = observations.filter(item => item.measurementPeriod === latestPeriod)
  const provinceCounts = latest.filter(item =>
    item.metricCode === 'AUDIENCE_COUNT' && item.geographyLevel !== 'COUNTRY')
  const sourceRows = provinceCounts.length
    ? provinceCounts
    : latest.filter(item => item.metricCode === 'AUDIENCE_COUNT' && item.geographyLevel === 'COUNTRY')
  if (!sourceRows.length) return aggregateAgeShares(latest.filter(item => item.metricCode === 'SHARE_PERCENT'))
  const buckets = new Map<string, number>(AGE_BUCKETS.map(([label]) => [label, 0]))
  sourceRows.forEach(item => {
    const bucket = ageBucket(item.dimensions.segment ?? '')
    if (bucket) buckets.set(bucket, (buckets.get(bucket) ?? 0) + item.metricValue)
  })
  const total = [...buckets.values()].reduce((sum, value) => sum + value, 0)
  if (total <= 0) return []
  return AGE_BUCKETS.map(([label]) => [label, Math.round((buckets.get(label) ?? 0) / total * 1000) / 10])
}

const AGE_BUCKETS = [
  ['15–24', 15, 24],
  ['25–34', 25, 34],
  ['35–49', 35, 49],
  ['50–64', 50, 64],
  ['65+', 65, 200],
] as const

function ageBucket(label: string) {
  const plus = label.match(/^(\d+)\+$/u)
  const range = label.match(/^(\d+)\s*[–-]\s*(\d+)$/u)
  const min = plus ? Number(plus[1]) : range ? Number(range[1]) : Number.NaN
  const max = plus ? 200 : range ? Number(range[2]) : Number.NaN
  if (!Number.isFinite(min) || !Number.isFinite(max)) return null
  return AGE_BUCKETS.find(([, low, high]) => min >= low && max <= high)?.[0] ?? null
}

function aggregateAgeShares(observations: AudienceResearchObservation[]): Distribution {
  const country = observations.filter(item => item.geographyLevel === 'COUNTRY')
  const source = country.length ? country : observations
  const buckets = new Map<string, number>(AGE_BUCKETS.map(([label]) => [label, 0]))
  source.forEach(item => {
    const bucket = ageBucket(item.dimensions.segment ?? '')
    if (bucket) buckets.set(bucket, (buckets.get(bucket) ?? 0) + item.metricValue)
  })
  const total = [...buckets.values()].reduce((sum, value) => sum + value, 0)
  if (total <= 0) return []
  return AGE_BUCKETS.map(([label]) => [label, Math.round((buckets.get(label) ?? 0) / total * 1000) / 10])
}

function socioEconomicDistribution(research: AudienceResearchContext | null): Distribution {
  const observations = (research?.observations ?? []).filter(item =>
    item.dimensions.group === 'LABOUR_MARKET' &&
    item.dimensions.segment === 'Labour force participation rate' &&
    item.metricCode === 'SHARE_PERCENT' && item.geographyLevel !== 'COUNTRY')
  return observations
    .sort((a, b) => a.geographyName.localeCompare(b.geographyName))
    .map(item => [item.geographyName, item.metricValue] as [string, number])
    .slice(0, 5)
}

function researchShareDistribution(research: AudienceResearchContext | null, groups: string[]): Distribution {
  const observations = (research?.observations ?? []).filter(item =>
    groups.includes(item.dimensions.group ?? '') && item.metricCode === 'SHARE_PERCENT')
  const country = observations.filter(item => item.geographyLevel === 'COUNTRY')
  const selected = country.length ? country : observations
  const values = new Map<string, number>()
  selected.forEach(item => {
    const label = item.dimensions.segment?.trim()
    if (label && !values.has(label)) values.set(label, item.metricValue)
  })
  return [...values.entries()].sort((a, b) => b[1] - a[1])
}

function digitalAccessRows(research: AudienceResearchContext | null): AudienceResearchObservation[] {
  const rows = (research?.observations ?? []).filter(item =>
    item.dimensions.group === 'HOUSEHOLD_INTERNET_ACCESS' &&
    item.metricCode === 'SHARE_PERCENT' && item.geographyLevel !== 'COUNTRY')
  const preferred = rows.filter(item => item.dimensions.segment === 'Any kind of access')
  const mobile = rows.filter(item => item.dimensions.segment === 'Mobile')
  return (preferred.length ? preferred : mobile.length ? mobile : rows.filter(item =>
    DIGITAL_ACCESS_SEGMENTS.includes(item.dimensions.segment as typeof DIGITAL_ACCESS_SEGMENTS[number])))
    .sort((a, b) => a.geographyName.localeCompare(b.geographyName))
}

function expandedResearchGeographies(research: AudienceResearchContext | null) {
  if (!research) return []
  const requested = new Set(research.requestedGeographies.map(value => value.toLowerCase()))
  return research.resolvedGeographies.filter(value => !requested.has(value.toLowerCase()))
}

function researchCoverageSentence(research: AudienceResearchContext | null) {
  const observations = research?.observations ?? []
  if (!observations.length) return null
  const sources = [...new Set(observations.map(item => item.sourceTitle))]
  const expanded = expandedResearchGeographies(research)
  return `${observations.length} governed aggregate research observations from ${sources.slice(0, 2).join(' and ')} are available as market context${expanded.length ? ` across ${expanded.join(', ')}` : ''}; they are not treated as audience-specific proof.`
}

function digitalAccessSentence(research: AudienceResearchContext | null) {
  const rows = digitalAccessRows(research).slice(0, 3)
  if (!rows.length) return null
  return `Household internet-access context is available for ${rows.map(item => `${item.geographyName} (${formatMetric(item.metricValue, '%')})`).join(', ')}; use it for market planning, not as proof of individual media preference.`
}

function geographyRows(audience: AudienceStrategy) {
  const counts = new Map<string, number>()
  audience.definitions.forEach(item => item.geographies.forEach(geography =>
    counts.set(geography, (counts.get(geography) ?? 0) + 1)))
  return [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8)
}

function geographySummary(count: number) {
  if (!count) return 'No verified areas yet'
  return `${count} retained audience area${count === 1 ? '' : 's'}`
}

function evidenceCoverageSentence(audience: AudienceStrategy) {
  const supported = audience.definitions.filter(item => item.evidenceItemIds.length > 0 ||
    item.referenceObservationIds.length > 0).length
  return supported === 0
    ? 'Audience identities come from the approved Brief; deeper demographic and behavioural traits remain audience-specific research gaps.'
    : `${supported} of ${audience.definitions.length} audience segments have retained segment evidence or governed segment-support observations.`
}

function geographyCoverageSentence(audience: AudienceStrategy) {
  const places = [...new Set(audience.definitions.flatMap(item => item.geographies))]
  return places.length
    ? `Retained audience geography currently covers ${places.map(displayLabel).join(', ')}.`
    : 'Audience geography remains a research gap.'
}

function hasMediaEvidence(audience: AudienceStrategy) {
  return audience.definitions.some(item => item.referenceObservationIds.length > 0)
}

function countValues(values: Array<string | null>) {
  const counts = new Map<string, number>()
  values.filter((value): value is string => Boolean(value)).forEach(value =>
    counts.set(value, (counts.get(value) ?? 0) + 1))
  return [...counts.entries()].sort((a, b) => b[1] - a[1])
}

function formatMetric(value: number, suffix: string) {
  if (suffix === '%') return `${value.toFixed(value % 1 === 0 ? 0 : 1)}%`
  return `${value}${suffix}`
}

function initials(value: string) {
  return value.trim().split(/\s+/).slice(0, 2).map(part => part[0]?.toUpperCase() ?? '').join('') || 'A'
}

function displayLabel(value: string) {
  return value.trim().replace(/[ .,:;!?]+$/, '')
}

function confidenceLabel(item: Segment) {
  if (item.confidence !== null) return `${Math.round(item.confidence * 100)}% confidence`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) return 'Client requirement'
  if (item.classification === masterDataCodes.evidenceClassifications.hypothesis) return 'Working hypothesis'
  return 'Not established'
}
