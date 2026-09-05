import { useEffect, useState, type ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import { bookingApi } from '../api/booking-client'
import type { Booking } from '../api/booking-schemas'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { inventoryApi } from '../api/inventory-client'
import type { InventoryProductPage } from '../api/inventory-schemas'
import { opportunityApi } from '../api/opportunity-client'
import type { CurrentUser, HumanTask, Tenant, Workspace } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { ExperienceSignals, type ExperienceSignal } from '../components/ExperienceSignals'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { mediaVisual } from '../planning/media-visuals'
import { formatMoney, formatNumber, humanizeCode } from '../presentation/format'

type DashboardData = {
  tenant: Tenant
  user: CurrentUser
  campaigns: Campaign[]
  bookings: Booking[]
  tasks: HumanTask[]
  inventory: InventoryProductPage | null
}

const thumbnails = [
  '/assets/media-inventory/out-of-home-real.jpg',
  '/assets/media-inventory/digital-real.jpg',
  '/assets/media-inventory/radio-real.jpg',
  '/assets/media-inventory/print-real.jpg',
  '/assets/media-inventory/television-real.jpg',
]

export function HomePage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <HomeData key={selected.tenantId} workspace={selected} />
}

function HomeData({ workspace }: { workspace: Workspace }) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void loadDashboard(workspace.tenantId)
      .then(value => { if (active) setData(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [workspace.tenantId])
  if (error) return <MessageState title="Your workspace could not be opened" message={error} />
  if (!data) return <LoadingState label={`Preparing ${workspace.name}`} />
  return <ApprovedDashboard data={data} />
}

async function loadDashboard(tenantId: string): Promise<DashboardData> {
  const [tenant, userProfile, campaigns, bookings, tasks, inventory] = await Promise.all([
    api.getTenant(tenantId),
    api.getCurrentUser(),
    campaignApi.list(tenantId).catch(() => []),
    bookingApi.list(tenantId).catch(() => []),
    opportunityApi.listTasks(tenantId).catch(() => []),
    inventoryApi.search(tenantId, {}).catch(() => null),
  ])
  return { tenant, user: userProfile.user, campaigns, bookings, tasks, inventory }
}

type DashboardView = ReturnType<typeof dashboardView>

function ApprovedDashboard({ data }: { data: DashboardData }) {
  const view = dashboardView(data)
  const [showGettingStarted, setShowGettingStarted] = useState(true)
  return <section className="approved-dashboard" aria-labelledby="home-title">
    <header className="approved-dashboard-greeting">
      <h1 id="home-title">{greeting()}, {firstName(data.user.displayName)} 👋</h1>
      <p>Your live workspace is prioritised around decisions, campaign movement and evidence-backed signals.</p>
    </header>
    <ExperienceSignals title="What needs attention now" signals={dashboardSignals(data, view)} />
    <DashboardKpis data={data} view={view} />
    <DashboardGrid data={data} view={view} />
    {showGettingStarted && <GettingStarted onDismiss={() => setShowGettingStarted(false)} />}
  </section>
}

function DashboardKpis({ data, view }: { data: DashboardData; view: DashboardView }) {
  return <div className="approved-kpi-grid">
    <KpiCard label="Active Campaigns" value={String(view.activeCampaigns.length)}
      trend={`${data.campaigns.length} total campaigns`}
      points="4,18 16,29 28,20 40,35 52,14 64,24 76,8" />
    <KpiCard label="Total Investment"
      value={formatMoney(view.totalInvestmentMinor, view.currency, 0)}
      trend="From persisted booking lines"
      points="4,32 16,27 28,29 40,20 52,24 64,15 76,27" />
    <KpiCard label="Planned Reach"
      value={view.reach > 0 ? compact(view.reach) : '—'}
      trend={view.reach > 0
        ? 'From reviewed campaign evidence' : 'No reviewed reach evidence yet'}
      points="4,29 16,19 28,24 40,14 52,18 64,9 76,20" />
    <KpiCard label="Avg. CPM"
      value={view.avgCpm === null
        ? '—' : formatMoney(Math.round(view.avgCpm * 100), view.currency, 2)}
      trend={view.avgCpm === null
        ? 'Waiting for spend + impressions' : 'Calculated from persisted evidence'}
      points="4,18 16,32 28,17 40,24 52,16 64,23 76,10" />
  </div>
}

function DashboardGrid({ data, view }: { data: DashboardData; view: DashboardView }) {
  return <div className="approved-dashboard-grid">
    <ActiveCampaignsPanel campaigns={view.recentCampaigns}
      totalCampaigns={data.campaigns.length} />
    <DashboardCentre view={view} />
    <DashboardRight data={data} campaigns={view.recentCampaigns} />
  </div>
}

function ActiveCampaignsPanel({ campaigns, totalCampaigns }: {
  campaigns: Campaign[]
  totalCampaigns: number
}) {
  return <Panel className="approved-active-campaigns" title="Active Campaigns"
    action={<Link to="/campaigns">View all</Link>}>
    {campaigns.length === 0
      ? <Empty message="No campaigns yet." />
      : campaigns.map((campaign, index) =>
          <CampaignRow key={campaign.id} campaign={campaign} index={index} />)}
    <footer>{campaigns.length > 0 &&
      <span>Showing {campaigns.length} of {totalCampaigns} campaigns</span>}
      <Link to="/campaigns">Go to campaigns →</Link></footer>
  </Panel>
}

function DashboardCentre({ view }: { view: DashboardView }) {
  return <div className="approved-dashboard-centre">
    <Panel title="Investment by Channel"
      action={<span className="approved-period-select">This month⌄</span>}>
      <InvestmentChart rows={view.channelSpend} total={view.totalInvestmentMinor}
        currency={view.currency} />
    </Panel>
    <Panel title="Top Inventory Updates">
      {view.recentInventory.length === 0
        ? <Empty message="No published inventory updates yet." />
        : <div className="approved-inventory-updates">
            {view.recentInventory.map(InventoryUpdate)}
          </div>}
      <Link className="approved-panel-link" to="/inventory">Go to inventory →</Link>
    </Panel>
  </div>
}

function InventoryUpdate(item: DashboardView['recentInventory'][number], index: number) {
  return <Link key={item.id} to={`/inventory/products/${item.id}`}>
    <span className={`approved-update-icon tone-${index + 1}`}>
      <Icon name={index === 0 ? 'brief' : index === 1 ? 'search' : 'inventory'} />
    </span>
    <div><strong>{item.name}</strong>
      <small>{item.geography} · {humanizeCode(item.channel, true)}</small></div>
    <em>{index === 0 ? 'NEW RATES' : 'AVAILABILITY'}</em>
    <time>{relative(item.updatedAtUtc)}</time>
  </Link>
}

function DashboardRight({ data, campaigns }: {
  data: DashboardData
  campaigns: Campaign[]
}) {
  return <div className="approved-dashboard-right">
    <Panel title="Recent Activity" action={<Link to="/campaigns">View all</Link>}>
      <div className="approved-activity-list">
        {campaigns.slice(0, 5).map((item, index) => <Link
          to={`/campaigns/${item.id}`} key={item.id}>
          <span className={`approved-activity-icon tone-${(index % 4) + 1}`}>✓</span>
          <div><strong>{item.title}</strong>
            <small>{humanizeCode(item.status, true)} · {relative(item.updatedAtUtc)}</small>
          </div>
        </Link>)}
      </div>
      {campaigns.length === 0 &&
        <Empty message="Activity will appear as campaigns move." />}
    </Panel>
    <Panel title="Tasks Needing Attention" action={<Link to="/tasks">View all</Link>}>
      <div className="approved-attention-list">{data.tasks.slice(0, 4).map(task =>
        <Link to={task.briefId
          ? `/briefs/${task.briefId}` : `/opportunities/${task.opportunityId}`}
          key={task.id}>
          <span>✓</span><div><strong>{task.title}</strong>
            <small>{task.whyItMatters}</small></div><em>1</em>
        </Link>)}</div>
      {data.tasks.length === 0 && <Empty message="No tasks need your attention." />}
    </Panel>
  </div>
}

function GettingStarted({ onDismiss }: { onDismiss: () => void }) {
  return <div className="approved-help-row">
    <article className="approved-getting-started">
      <button type="button" aria-label="Dismiss" onClick={onDismiss}>×</button>
      <h2>Need help getting started?</h2>
      <p>Create a brief, explore inventory or let Adverti Assistant guide you.</p>
      <div><Link to="/briefs/new"><Icon name="brief" /><span>
        <strong>Create Brief</strong><small>Start a new campaign brief</small>
      </span></Link>
      <Link to="/inventory"><Icon name="inventory" /><span>
        <strong>Explore Inventory</strong><small>Search sites and rates</small>
      </span></Link>
      <Link to="/opportunities"><span className="spark">✦</span><span>
        <strong>Ask Adverti</strong><small>Get evidence-led insights</small>
      </span></Link></div>
    </article>
  </div>
}

function dashboardView(data: DashboardData) {
  const activeCampaigns = data.campaigns
    .filter(item => item.status !== masterDataCodes.lifecycleStatuses.completed &&
      item.status !== masterDataCodes.lifecycleStatuses.cancelled)
  const totalInvestmentMinor = data.bookings
    .reduce((sum, item) => sum + (item.clientPriceMinor ?? 0), 0)
  const currency = data.bookings.find(item => item.currency)?.currency ??
    data.tenant.currencyCode
  const reach = metricTotal(data.campaigns, masterDataCodes.performanceMetricTypes.reach)
  const impressions = metricTotal(data.campaigns, masterDataCodes.performanceMetricTypes.impressions)
  return {
    activeCampaigns,
    totalInvestmentMinor,
    currency,
    reach,
    avgCpm: impressions > 0
      ? totalInvestmentMinor / 100 * 1000 / impressions : null,
    channelSpend: investmentByChannel(data.bookings),
    recentCampaigns: [...data.campaigns]
      .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)).slice(0, 5),
    recentInventory: [...(data.inventory?.items ?? [])]
      .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)).slice(0, 3),
  }
}

function KpiCard({ label, value, trend, points }: { label: string; value: string; trend: string; points: string }) {
  return <article className="approved-kpi-card"><div><span>{label}</span><strong>{value}</strong><small>{trend}</small></div>
    <svg viewBox="0 0 80 42" aria-hidden="true"><polyline points={points} /></svg></article>
}

function Panel({ title, action, children, className = '' }: { title: string; action?: ReactNode; children: ReactNode; className?: string }) {
  return <article className={`approved-panel ${className}`}><header><h2>{title}</h2>{action}</header>{children}</article>
}

function CampaignRow({ campaign, index }: { campaign: Campaign; index: number }) {
  return <Link className="approved-campaign-row" to={`/campaigns/${campaign.id}`}>
    <img src={thumbnails[index % thumbnails.length]} alt="" /><div><strong>{campaign.title}</strong>
      <small>{formatDateRange(campaign.startDate, campaign.endDate)}</small><em>{humanizeCode(campaign.status, true)}</em></div>
    <span className="approved-campaign-stage"><strong>{humanizeCode(campaign.status, true)}</strong><small>Persisted campaign state</small></span>
    <b aria-hidden="true">→</b>
  </Link>
}

function InvestmentChart({ rows, total, currency }: { rows: Array<[string, number]>; total: number; currency: string }) {
  const gradient = rows.length === 0 ? '#eef1f7 0 100%' : buildGradient(rows, total)
  return <div className="approved-investment-chart"><div className="approved-donut" style={{ background: `conic-gradient(${gradient})` }}>
    <span><strong>{formatMoney(total, currency, 0)}</strong><small>Total</small></span></div>
    <div className="approved-channel-legend">{rows.length === 0 ? <Empty message="No booked spend by channel yet." /> : rows.map(([channel, amount]) => {
      const visual = mediaVisual(channel)
      return <div key={channel}><span style={{ background: visual.color }} /><strong>{visual.label}</strong>
        <small>{formatMoney(amount, currency, 0)} ({Math.round(amount / Math.max(total, 1) * 100)}%)</small></div>
    })}</div></div>
}

function Empty({ message }: { message: string }) { return <p className="approved-empty">{message}</p> }
function metricTotal(campaigns: Campaign[], metric: string) { return campaigns.flatMap(c => c.performanceEvidence).flatMap(e => e.metrics).filter(m => m.metricType === metric).reduce((sum, m) => sum + m.value, 0) }
function investmentByChannel(bookings: Booking[]) { const totals = new Map<string, number>(); for (const item of bookings) totals.set(item.channel, (totals.get(item.channel) ?? 0) + (item.clientPriceMinor ?? 0)); return [...totals.entries()].sort((a, b) => b[1] - a[1]).slice(0, 6) }
function compact(value: number) { if (value >= 1_000_000) return `${formatNumber(value / 1_000_000, 1)}M`; if (value >= 1_000) return `${formatNumber(value / 1_000, 1)}K`; return formatNumber(value) }
function relative(value: string) { const minutes = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 60000)); if (minutes < 60) return `${minutes}m ago`; const hours = Math.round(minutes / 60); if (hours < 24) return `${hours}h ago`; return `${Math.round(hours / 24)}d ago` }
function formatDateRange(start: string, end: string) { return `${new Date(start).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${new Date(end).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}` }
function buildGradient(rows: Array<[string, number]>, total: number) { let cursor = 0; return rows.map(([channel, amount]) => { const start = cursor; cursor += amount / Math.max(total, 1) * 100; return `${mediaVisual(channel).color} ${start}% ${cursor}%` }).join(', ') }
function dashboardSignals(data: DashboardData, view: DashboardView): ExperienceSignal[] {
  const topChannel = view.channelSpend[0]
  return [
    {
      label: 'Human decisions', value: `${data.tasks.length} waiting`, icon: 'tasks',
      tone: data.tasks.length > 0 ? 'warning' : 'positive',
      detail: data.tasks.length > 0 ? 'Persisted approvals or corrections need attention.' : 'No assigned human checkpoints are currently waiting.',
      why: 'This count comes only from persisted HumanTask records for the current workspace.',
    },
    {
      label: 'Campaign movement', value: `${view.activeCampaigns.length} active`, icon: 'plan', tone: 'violet',
      detail: `${data.campaigns.length} campaign${data.campaigns.length === 1 ? '' : 's'} exist in this workspace.`,
      why: 'Completed and cancelled campaigns are excluded from the active count.',
    },
    {
      label: 'Investment concentration',
      value: topChannel ? mediaVisual(topChannel[0]).label : 'No spend yet', icon: 'chart', tone: 'blue',
      detail: topChannel ? `${Math.round(topChannel[1] / Math.max(view.totalInvestmentMinor, 1) * 100)}% of persisted booked investment is in this channel.` : 'Booked channel investment will appear here as commercial lines are confirmed.',
      why: 'The signal is calculated from persisted booking client-price values, not a forecast.',
    },
    {
      label: 'Evidence readiness', value: view.reach > 0 ? 'Reach available' : 'Reach pending', icon: 'evidence',
      tone: view.reach > 0 ? 'positive' : 'neutral',
      detail: view.reach > 0 ? `${compact(view.reach)} reviewed reach is recorded.` : 'No reviewed reach evidence is available yet.',
      why: 'Advertified only surfaces reach when reviewed campaign evidence contains a reach metric.',
    },
  ]
}
function greeting() { const hour = new Date().getHours(); if (hour < 12) return 'Good morning'; if (hour < 18) return 'Good afternoon'; return 'Good evening' }
function firstName(displayName: string) { return displayName.trim().split(/\s+/)[0] || 'there' }
