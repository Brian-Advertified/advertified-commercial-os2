import { Link } from 'react-router-dom'
import type { Booking } from '../api/booking-schemas'
import type { Campaign } from '../api/campaign-schemas'
import type { DashboardData } from './dashboard-data'
import { Icon, type IconName } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { mediaVisual } from '../planning/media-visuals'
import { formatMoney, humanizeCode } from '../presentation/format'
import { bookingInvestment } from './dashboard-metrics'

export function ConnectedOperatorHome({ data }: { data: DashboardData }) {
  const active = activeCampaigns(data.campaigns)
  const investment = bookingInvestment(data.bookings, data.tenant.currencyCode)
  return <section className="connected-home" aria-labelledby="connected-home-title">
    <HomeHero name={firstName(data.user.displayName)} />
    <HomeMetrics data={data} activeCampaigns={active.length} investment={investment} />
    <div className="connected-home-primary-grid">
      <RecentCampaigns campaigns={active.slice(0, 4)} bookings={data.bookings} currency={data.tenant.currencyCode} />
      <ChannelActivity bookings={data.bookings} currency={data.tenant.currencyCode} />
      <InsightPanel data={data} />
    </div>
    <div className="connected-home-secondary-grid">
      <OpportunityPanel data={data} />
      <TaskPanel data={data} />
      <Milestones campaigns={data.campaigns} />
    </div>
  </section>
}

function HomeHero({ name }: { name: string }) {
  return <header className="connected-home-hero"><div className="connected-home-hero-copy">
    <p className="eyebrow">Welcome back</p><h1 id="connected-home-title">Good {daypart()}, {name}</h1>
    <p>Turn ideas into real-world impact across every media channel.</p></div>
    <div className="connected-home-hero-art"><img src="/assets/media-inventory/out-of-home-real.jpg"
      alt="South African city and outdoor media landscape" /><strong>Smarter advertising<br />for a brighter<br />South Africa.</strong><i /></div>
  </header>
}

function HomeMetrics({ data, activeCampaigns, investment }: {
  data: DashboardData
  activeCampaigns: number
  investment: ReturnType<typeof bookingInvestment>
}) {
  const waiting = data.proposals.filter(item => item.status === masterDataCodes.lifecycleStatuses.sent ||
    item.status === masterDataCodes.lifecycleStatuses.inReview).length
  const total = investment.totalMinor
  return <section className="connected-home-metrics" aria-label="Workspace summary">
    <Metric icon="chart" value={String(activeCampaigns)} label="Active campaigns" to="/campaigns" />
    <Metric icon="proposal" value={String(waiting)} label="Proposals awaiting action" to="/proposals" />
    <Metric icon="target" value={String(data.opportunities.length)} label="Opportunities" to="/opportunities" />
    <Metric icon="money" value={total === null ? '—' : formatMoney(total, data.tenant.currencyCode, 0)}
      label="Confirmed media value" to="/bookings" />
    <Link className="connected-home-new primary-button" to="/briefs/new"><Icon name="plus" /> New Campaign</Link>
  </section>
}

function Metric({ icon, value, label, to }: { icon: IconName; value: string; label: string; to: string }) {
  return <Link className="connected-home-metric" to={to}><span><Icon name={icon} /></span>
    <div><strong>{value}</strong><small>{label}</small></div><b>→</b></Link>
}

function RecentCampaigns({ campaigns, bookings, currency }: {
  campaigns: Campaign[]; bookings: Booking[]; currency: string
}) {
  return <Panel className="connected-home-recent" title="Recent Campaigns" action={<Link to="/campaigns">View all →</Link>}>
    {campaigns.length === 0 ? <Empty>There are no active campaigns yet.</Empty> :
      campaigns.map(campaign => <CampaignItem key={campaign.id} campaign={campaign}
        bookings={bookings.filter(item => item.planVersionId === campaign.planVersionId)} currency={currency} />)}
  </Panel>
}

function CampaignItem({ campaign, bookings, currency }: { campaign: Campaign; bookings: Booking[]; currency: string }) {
  const value = bookings.reduce((sum, item) => sum + (item.clientPriceMinor ?? 0), 0)
  const channel = bookings[0]?.channel
  return <Link className="connected-home-campaign-row" to={`/campaigns/${campaign.id}`}>
    <span className="connected-home-thumb"><img src={channelArtwork(channel)} alt="" /></span>
    <div><strong>{campaign.title}</strong><small>{campaignChannels(bookings)}</small>
      <em>{[...new Set(bookings.map(item => item.geography))].slice(0, 3).join(', ') || 'Campaign geography pending'}</em></div>
    <span className="connected-home-state">{humanizeCode(campaign.status, true)}</span>
    <span className="connected-home-value"><strong>{value > 0 ? formatMoney(value, currency, 0) : '—'}</strong>
      <small>{dateRange(campaign.startDate, campaign.endDate)}</small></span>
  </Link>
}

function ChannelActivity({ bookings, currency }: { bookings: Booking[]; currency: string }) {
  const rows = channelTotals(bookings)
  const max = Math.max(...rows.map(item => item.amount), 1)
  return <Panel className="connected-home-performance" title="Media Channel Activity"
    action={<span className="connected-home-select">Confirmed value</span>}>
    {rows.length === 0 ? <Empty>No confirmed media value is available yet.</Empty> :
      <div className="connected-home-bars">{rows.slice(0, 6).map(item => <div key={item.channel}>
        <strong>{formatCompactMoney(item.amount, currency)}</strong><i><b style={{ height: `${Math.max(12, item.amount / max * 100)}%`, background: mediaVisual(item.channel).color }} /></i>
        <span><Icon name="chart" />{mediaVisual(item.channel).label}</span></div>)}</div>}
  </Panel>
}

function InsightPanel({ data }: { data: DashboardData }) {
  const items = insightItems(data)
  return <Panel className="connected-home-insights" title="AI Insights">
    <div>{items.map(item => <article key={item.title}><span><Icon name={item.icon} /></span><div>
      <strong>{item.title}</strong><p>{item.copy}</p></div></article>)}</div>
    <Link className="connected-home-panel-link" to="/measurement">View detailed insights →</Link>
  </Panel>
}

function OpportunityPanel({ data }: { data: DashboardData }) {
  return <Panel className="connected-home-opportunities" title="Active Opportunities" action={<Link to="/opportunities">View all →</Link>}>
    {data.opportunities.length === 0 ? <Empty>No active commercial opportunities are visible.</Empty> :
      data.opportunities.slice(0, 4).map(item => <Link to={`/opportunities/${item.id}`} key={item.id}>
        <span className="connected-home-thumb"><img src="/assets/media-inventory/out-of-home-real.jpg" alt="" /></span>
        <div><strong>{item.title}</strong><small>{item.objectiveSummary ?? item.problemSummary ?? 'Qualification in progress'}</small></div>
        <em>{humanizeCode(item.stage, true)}</em></Link>)}
  </Panel>
}

function TaskPanel({ data }: { data: DashboardData }) {
  return <Panel className="connected-home-tasks" title="Tasks & Approvals"
    action={<Link to="/tasks">View all →</Link>}>
    {data.tasks.length === 0 ? <Empty>No tasks are currently assigned to you.</Empty> :
      data.tasks.slice(0, 5).map(task => <Link to={task.briefId ? `/briefs/${task.briefId}` : task.opportunityId
        ? `/opportunities/${task.opportunityId}` : '/tasks'} key={task.id}>
        <span><Icon name="tasks" /></span><div><strong>{task.title}</strong><small>{task.whyItMatters}</small></div>
        <b>Open</b></Link>)}
  </Panel>
}

function Milestones({ campaigns }: { campaigns: Campaign[] }) {
  const entries = milestoneRows(campaigns)
  return <Panel className="connected-home-milestones" title="Upcoming Milestones" action={<Link to="/campaigns">View all →</Link>}>
    {entries.length === 0 ? <Empty>No upcoming campaign milestones are retained.</Empty> : <div>{entries.map(entry => <Link
      key={`${entry.campaign.id}-${entry.kind}`} to={`/campaigns/${entry.campaign.id}`}>
      <time dateTime={entry.date}><strong>{day(entry.date)}</strong><small>{month(entry.date)}</small></time><i />
      <div><strong>{entry.label}</strong><small>{entry.campaign.title}</small></div></Link>)}</div>}
  </Panel>
}

function Panel({ title, action, children, className = '' }: {
  title: string; action?: React.ReactNode; children: React.ReactNode; className?: string
}) {
  return <section className={`connected-home-panel ${className}`}><header><h2>{title}</h2>{action}</header>{children}</section>
}

function Empty({ children }: { children: React.ReactNode }) {
  return <p className="connected-home-empty">{children}</p>
}

function activeCampaigns(campaigns: Campaign[]) {
  return campaigns.filter(item => item.status !== masterDataCodes.lifecycleStatuses.completed &&
    item.status !== masterDataCodes.lifecycleStatuses.cancelled)
}

function channelTotals(bookings: Booking[]) {
  const totals = new Map<string, number>()
  bookings.forEach(item => totals.set(item.channel, (totals.get(item.channel) ?? 0) + (item.clientPriceMinor ?? 0)))
  return [...totals.entries()].map(([channel, amount]) => ({ channel, amount })).filter(item => item.amount > 0)
    .sort((a, b) => b.amount - a.amount)
}

function insightItems(data: DashboardData): Array<{ icon: IconName; title: string; copy: string }> {
  const task = data.tasks[0]
  const opportunity = data.opportunities[0]
  const planning = data.planning.find(item => item.mediaPlanStatus === masterDataCodes.lifecycleStatuses.approved)
  return [
    { icon: 'target', title: opportunity ? 'Commercial opportunity' : 'Opportunity watch',
      copy: opportunity ? `${opportunity.title} is currently ${humanizeCode(opportunity.stage, true).toLowerCase()}.` : 'No current opportunity signal requires action.' },
    { icon: 'users', title: 'Audience & planning', copy: planning
      ? `An approved media plan exists for ${planning.briefTitle}.` : 'Approved audience and planning evidence will surface here as campaigns progress.' },
    { icon: 'tasks', title: task ? 'Recommended next action' : 'Decision queue clear',
      copy: task ? `${task.title}: ${task.whyItMatters}` : 'No assigned human checkpoint is currently waiting.' },
  ]
}

function milestoneRows(campaigns: Campaign[]) {
  const now = new Date().toISOString().slice(0, 10)
  const rows = campaigns.flatMap(campaign => [
    { campaign, kind: 'start', date: campaign.startDate, label: 'Campaign launch' },
    { campaign, kind: 'end', date: campaign.endDate, label: 'Campaign end' },
  ]).filter(item => item.date >= now)
  return rows.sort((a, b) => a.date.localeCompare(b.date)).slice(0, 5)
}

function campaignChannels(bookings: Booking[]) {
  const labels = [...new Set(bookings.map(item => mediaVisual(item.channel).label))]
  return labels.length ? labels.join(' · ') : 'Media plan retained'
}

function channelArtwork(channel?: string) {
  const label = channel ? mediaVisual(channel).tone : 'ooh'
  const file = label === 'radio' ? 'radio-real.jpg' : label === 'tv' ? 'television-real.jpg' :
    label === 'print' ? 'print-real.jpg' : label === 'digital' || label === 'social' ? 'digital-real.jpg' :
      label === 'experiential' ? 'experiential-real.jpg' : 'out-of-home-real.jpg'
  return `/assets/media-inventory/${file}`
}

function formatCompactMoney(amount: number, currency: string) {
  if (amount >= 100_000_000) return `${currency} ${(amount / 100_000_000).toFixed(1)}M`
  if (amount >= 100_000) return `${currency} ${(amount / 100_000).toFixed(0)}K`
  return formatMoney(amount, currency, 0)
}

function dateRange(start: string, end: string) {
  return `${new Date(start).toLocaleDateString(undefined, { month: 'short' })} – ${new Date(end).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })}`
}
function firstName(name: string) { return name.trim().split(/\s+/)[0] || 'there' }
function daypart() { const h = new Date().getHours(); return h < 12 ? 'morning' : h < 18 ? 'afternoon' : 'evening' }
function day(value: string) { return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { day: '2-digit' }) }
function month(value: string) { return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, { month: 'short' }).toUpperCase() }
