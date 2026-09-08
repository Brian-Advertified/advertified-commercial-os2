import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import type { CampaignBriefSummary } from '../api/schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'

type BriefFilter = 'ALL' | 'APPROVED_BRIEFS' | 'DECISION' | 'IN_PROGRESS'

export function BriefsWorkspace({ briefs }: { briefs: CampaignBriefSummary[] }) {
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<BriefFilter>('ALL')
  const visible = useMemo(() => filterBriefs(briefs, query, filter), [briefs, query, filter])
  const counts = briefCounts(briefs)
  return <section className="briefs-index" aria-labelledby="briefs-index-title">
    <BriefsHeader counts={counts} />
    <BriefsToolbar query={query} filter={filter} resultCount={visible.length}
      onQuery={setQuery} onFilter={setFilter} />
    {visible.length > 0
      ? <div className="briefs-index-grid">{visible.map(brief =>
        <BriefCard key={brief.id} brief={brief} />)}</div>
      : <BriefsEmpty hasBriefs={briefs.length > 0} />}
  </section>
}

function BriefsHeader({ counts }: { counts: ReturnType<typeof briefCounts> }) {
  return <header className="briefs-index-hero">
    <div className="briefs-index-hero__copy">
      <span className="eyebrow">CAMPAIGN COMMAND CENTRE</span>
      <h1 id="briefs-index-title">Briefs</h1>
      <p>Turn each client request into an approved, evidence-led campaign direction.</p>
      <Link className="primary-button" to="/briefs/new">Create new Brief</Link>
    </div>
    <dl className="briefs-index-metrics">
      <Metric value={counts.total} label="Total Briefs" />
      <Metric value={counts.approved} label="Approved" tone="positive" />
      <Metric value={counts.decision} label="Awaiting decision" tone="warning" />
      <Metric value={counts.inProgress} label="In progress" />
    </dl>
  </header>
}

function Metric({ value, label, tone = '' }: {
  value: number; label: string; tone?: string
}) {
  return <div className={tone ? `is-${tone}` : undefined}>
    <dd>{value.toLocaleString()}</dd><dt>{label}</dt>
  </div>
}

function BriefsToolbar({ query, filter, resultCount, onQuery, onFilter }: {
  query: string
  filter: BriefFilter
  resultCount: number
  onQuery: (value: string) => void
  onFilter: (value: BriefFilter) => void
}) {
  return <div className="briefs-index-toolbar">
    <label><span>Find a Brief</span>
      <input type="search" value={query} placeholder="Search campaign or client"
        onChange={event => onQuery(event.target.value)} /></label>
    <label><span>Stage</span>
      <select value={filter} onChange={event => onFilter(event.target.value as BriefFilter)}>
        <option value="ALL">All stages</option><option value="APPROVED_BRIEFS">Approved</option>
        <option value="DECISION">Awaiting decision</option>
        <option value="IN_PROGRESS">In progress</option>
      </select></label>
    <p><strong>{resultCount}</strong> {resultCount === 1 ? 'Brief' : 'Briefs'}</p>
  </div>
}

function BriefCard({ brief }: { brief: CampaignBriefSummary }) {
  const stage = briefStage(brief)
  return <Link className="briefs-index-card" to={`/briefs/${brief.id}`}>
    <div className="briefs-index-card__top">
      <span className={`briefs-index-card__icon is-${stage.tone}`}><Icon name="brief" /></span>
      <span className={`briefs-index-status is-${stage.tone}`}>{stage.label}</span>
    </div>
    <div className="briefs-index-card__copy">
      <span>{brief.clientName}</span><h2>{brief.title}</h2><p>{stage.next}</p>
    </div>
    <footer><time>Updated {formatDateTime(brief.updatedAtUtc)}</time>
      <span>Open Brief <Icon name="arrow" /></span></footer>
  </Link>
}

function BriefsEmpty({ hasBriefs }: { hasBriefs: boolean }) {
  return <article className="briefs-index-empty"><span><Icon name="brief" /></span>
    <h2>{hasBriefs ? 'No Briefs match these filters' : 'Start with the client request'}</h2>
    <p>{hasBriefs ? 'Clear the search or choose another stage.'
      : 'Create a Brief to retain the source, surface unknowns and begin the campaign journey.'}</p>
    {!hasBriefs && <Link className="primary-button" to="/briefs/new">Create first Brief</Link>}
  </article>
}

function briefStage(brief: CampaignBriefSummary) {
  if (brief.approvedVersionId || brief.status === masterDataCodes.lifecycleStatuses.approved) {
    return { label: 'Approved', tone: 'positive', next: 'Ready for audience strategy and planning.' }
  }
  if (brief.readyVersionId) {
    return { label: 'Awaiting decision', tone: 'warning', next: 'Review the retained facts and approve the Brief.' }
  }
  if (brief.currentDraftVersionId) {
    return { label: 'In progress', tone: 'active', next: 'Complete the structured Brief and resolve open items.' }
  }
  return { label: humanizeCode(brief.status, true), tone: 'neutral',
    next: 'The source is retained; complete its structured campaign Brief.' }
}

function briefCounts(briefs: CampaignBriefSummary[]) {
  const approved = briefs.filter(item => item.approvedVersionId).length
  const decision = briefs.filter(item => !item.approvedVersionId && item.readyVersionId).length
  return { total: briefs.length, approved, decision,
    inProgress: briefs.length - approved - decision }
}

function filterBriefs(briefs: CampaignBriefSummary[], query: string, filter: BriefFilter) {
  const needle = query.trim().toLocaleLowerCase()
  return briefs.filter(brief => {
    const stage = briefStage(brief).label
    const matchesText = !needle || `${brief.title} ${brief.clientName}`.toLocaleLowerCase().includes(needle)
    const matchesStage = filter === 'ALL' ||
      (filter === 'APPROVED_BRIEFS' && stage === 'Approved') ||
      (filter === 'DECISION' && stage === 'Awaiting decision') ||
      (filter === 'IN_PROGRESS' && stage !== 'Approved' && stage !== 'Awaiting decision')
    return matchesText && matchesStage
  })
}
