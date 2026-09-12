import { useEffect, useState, type ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import type { Campaign } from '../api/campaign-schemas'
import type { Workspace } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { ExperienceSignals, type ExperienceSignal } from '../components/ExperienceSignals'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { homeCopy, investmentDescription } from '../content/home-copy'
import { masterDataCodes } from '../generated/master-data-codes'
import { DashboardCommercialProof } from '../home/DashboardCommercialProof'
import { homeAudience, loadDashboard, type DashboardData } from '../home/dashboard-data'
import { bookingInvestment } from '../home/dashboard-metrics'
import { RoleHomeDashboard } from '../home/RoleHomeDashboard'
import { mediaVisual } from '../planning/media-visuals'
import { formatMoney, humanizeCode } from '../presentation/format'

export function HomePage() {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!homeAudience(selected.roleCode)) return <MessageState
    title={homeCopy.noAccessTitle} message={homeCopy.noAccessMessage} />
  return <HomeData key={`${selected.tenantId}:${selected.roleCode}`} workspace={selected} />
}

function HomeData({ workspace }: { workspace: Workspace }) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    let active = true
    void loadDashboard(workspace)
      .then(value => { if (active) setData(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [workspace, attempt])
  function retry() { setData(null); setError(null); setAttempt(value => value + 1) }
  if (error) return <>
    <MessageState title={homeCopy.unavailableTitle} message={error} />
    <p>{homeCopy.unavailableNote}</p>
    <button type="button" className="primary-button" onClick={retry}>{homeCopy.retry}</button>
  </>
  if (!data) return <LoadingState label={`Preparing ${workspace.name}`} />
  if (homeAudience(workspace.roleCode) !== 'operator') return <RoleHomeDashboard
    roleCode={workspace.roleCode} displayName={data.user.displayName}
    workspaceName={workspace.name} currency={data.tenant.currencyCode}
    campaigns={data.campaigns} bookings={data.bookings} tasks={data.tasks}
    planning={data.planning} proposals={data.proposals} rfqs={data.rfqs}
    inventory={data.inventory} />
  return <ApprovedDashboard data={data} />
}

type DashboardView = ReturnType<typeof dashboardView>

function ApprovedDashboard({ data }: { data: DashboardData }) {
  const view = dashboardView(data)
  const newWorkspace = data.campaigns.length === 0 && data.bookings.length === 0 &&
    data.planning.length === 0 && data.proposals.length === 0
  return <section className="approved-dashboard" aria-labelledby="home-title">
    <header className="approved-dashboard-greeting">
      <h1 id="home-title">{greeting()}, {firstName(data.user.displayName)} 👋</h1>
      <p>{newWorkspace ? homeCopy.emptyIntro : homeCopy.activeIntro}</p>
    </header>
    {newWorkspace ? <NewWorkspaceHome data={data} view={view} /> : <>
      <ExperienceSignals title="What needs attention now" signals={dashboardSignals(data, view)} />
      <DashboardCommercialProof planning={data.planning} proposals={data.proposals}
        campaigns={data.campaigns} bookings={data.bookings} />
      <DashboardKpis data={data} view={view} />
      <DashboardGrid data={data} view={view} />
    </>}
  </section>
}

function NewWorkspaceHome({ data, view }: { data: DashboardData; view: DashboardView }) {
  return <div className="approved-new-workspace-home">
    {data.tasks.length > 0 && <ExperienceSignals title="What needs attention now"
      signals={dashboardSignals(data, view)} />}
    <article className="approved-launchpad">
      <div><p className="eyebrow">{homeCopy.startEyebrow}</p><h2>{homeCopy.startTitle}</h2>
        <p>{homeCopy.startDescription}</p></div>
      <Link className="primary-button" to="/briefs/new">{homeCopy.startAction} <span aria-hidden="true">→</span></Link>
    </article>
  </div>
}

function DashboardKpis({ data, view }: { data: DashboardData; view: DashboardView }) {
  return <div className="approved-kpi-grid">
    <KpiCard label={homeCopy.activeCampaigns} value={String(view.activeCampaigns.length)}
      detail={`${data.campaigns.length} total campaigns`} />
    <KpiCard label={homeCopy.confirmedMedia}
      value={view.investment.totalMinor === null ? '—' : formatMoney(view.investment.totalMinor, view.currency, 0)}
      detail={view.investment.totalMinor === null ? homeCopy.unknownAmount :
        investmentDescription(view.currency, view.investment.otherCurrencies)} />
    <KpiCard label={homeCopy.awaitingDecision} value={String(view.pendingDecisions)} detail={homeCopy.decisionNote} />
    <KpiCard label={homeCopy.approvedPlans} value={String(view.approvedPlans)} detail={homeCopy.planNote} />
  </div>
}

function DashboardGrid({ data, view }: { data: DashboardData; view: DashboardView }) {
  return <div className="approved-dashboard-grid">
    <ActiveCampaignsPanel campaigns={view.recentActiveCampaigns} totalCampaigns={view.activeCampaigns.length} />
    <div className="approved-dashboard-centre">
      <Panel title={homeCopy.investmentTitle} action={<span>{view.currency}</span>}>
        <InvestmentChart investment={view.investment} currency={view.currency} />
      </Panel>
      <Panel title={homeCopy.inventoryTitle}>
        <p className="approved-empty">{homeCopy.inventoryNote}</p>
        {view.recentInventory.length === 0 ? <Empty message={homeCopy.inventoryEmpty} /> :
          <div className="approved-inventory-updates">{view.recentInventory.map(InventoryUpdate)}</div>}
        <Link className="approved-panel-link" to="/inventory">Go to inventory →</Link>
      </Panel>
    </div>
    <DashboardRight data={data} campaigns={view.recentCampaigns} />
  </div>
}

function ActiveCampaignsPanel({ campaigns, totalCampaigns }: { campaigns: Campaign[]; totalCampaigns: number }) {
  return <Panel className="approved-active-campaigns" title={homeCopy.activeCampaigns}
    action={<Link to="/campaigns">View all</Link>}>
    {campaigns.length === 0 ? <Empty message={homeCopy.noCampaigns} /> :
      campaigns.map(campaign => <CampaignRow key={campaign.id} campaign={campaign} />)}
    <footer>{campaigns.length > 0 && <span>Showing {campaigns.length} of {totalCampaigns} active campaigns</span>}
      <Link to="/campaigns">Go to campaigns →</Link></footer>
  </Panel>
}

function InventoryUpdate(item: DashboardView['recentInventory'][number]) {
  return <Link key={item.id} to={`/inventory/products/${item.id}`}>
    <span className="approved-update-icon tone-1"><Icon name="inventory" /></span>
    <div><strong>{item.name}</strong><small>{item.geography} · {humanizeCode(item.channel, true)}</small></div>
    <time dateTime={item.updatedAtUtc}>{relative(item.updatedAtUtc)}</time>
  </Link>
}

function DashboardRight({ data, campaigns }: { data: DashboardData; campaigns: Campaign[] }) {
  return <div className="approved-dashboard-right">
    <Panel title="Recent Activity" action={<Link to="/campaigns">View all</Link>}>
      <div className="approved-activity-list">{campaigns.slice(0, 5).map(item =>
        <Link to={`/campaigns/${item.id}`} key={item.id}>
          <span className="approved-activity-icon tone-1"><Icon name="plan" /></span>
          <div><strong>{item.title}</strong><small>{humanizeCode(item.status, true)} · {relative(item.updatedAtUtc)}</small></div>
        </Link>)}</div>
      {campaigns.length === 0 && <Empty message={homeCopy.activityEmpty} />}
    </Panel>
    <Panel title="Tasks Needing Attention" action={<Link to="/tasks">View all</Link>}>
      <div className="approved-attention-list">{data.tasks.slice(0, 4).map(task =>
        <Link to={task.briefId ? `/briefs/${task.briefId}` :
          task.opportunityId ? `/opportunities/${task.opportunityId}` : '/tasks'} key={task.id}>
          <Icon name="tasks" /><div><strong>{task.title}</strong><small>{task.whyItMatters}</small></div>
        </Link>)}</div>
      <Empty message={data.tasks.length === 0 ? homeCopy.noTasks : homeCopy.tasksNote} />
    </Panel>
  </div>
}

function dashboardView(data: DashboardData) {
  const activeCampaigns = data.campaigns.filter(item =>
    item.status !== masterDataCodes.lifecycleStatuses.completed && item.status !== masterDataCodes.lifecycleStatuses.cancelled)
  const currency = data.tenant.currencyCode
  return {
    activeCampaigns, currency,
    investment: bookingInvestment(data.bookings, currency),
    pendingDecisions: data.proposals.filter(item => item.status === masterDataCodes.lifecycleStatuses.sent).length,
    approvedPlans: data.planning.filter(item => item.mediaPlanStatus === masterDataCodes.lifecycleStatuses.approved).length,
    recentActiveCampaigns: recentCampaigns(activeCampaigns),
    recentCampaigns: recentCampaigns(data.campaigns),
    recentInventory: [...(data.inventory?.items ?? [])]
      .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)).slice(0, 3),
  }
}

function KpiCard({ label, value, detail }: { label: string; value: string; detail: string }) {
  return <article className="approved-kpi-card"><div><span>{label}</span><strong>{value}</strong><small>{detail}</small></div></article>
}

function Panel({ title, action, children, className = '' }: { title: string; action?: ReactNode; children: ReactNode; className?: string }) {
  return <article className={`approved-panel ${className}`}><header><h2>{title}</h2>{action}</header>{children}</article>
}

function CampaignRow({ campaign }: { campaign: Campaign }) {
  return <Link className="approved-campaign-row" to={`/campaigns/${campaign.id}`}>
    <span className="approved-update-icon tone-1"><Icon name="plan" /></span>
    <div><strong>{campaign.title}</strong><small>{formatDateRange(campaign.startDate, campaign.endDate)}</small>
      <em>{humanizeCode(campaign.status, true)}</em></div>
    <span className="approved-campaign-stage"><strong>{humanizeCode(campaign.status, true)}</strong><small>{homeCopy.campaignState}</small></span>
    <b aria-hidden="true">→</b>
  </Link>
}

function InvestmentChart({ investment, currency }: { investment: ReturnType<typeof bookingInvestment>; currency: string }) {
  const { channels: rows, totalMinor: total } = investment
  if (total === null) return <Empty message={homeCopy.investmentUnavailable} />
  if (total === 0) return <Empty message={`${homeCopy.investmentEmpty} ${investmentDescription(currency, investment.otherCurrencies)}`} />
  return <><div className="approved-investment-chart">
    <div className="approved-donut" aria-hidden="true" style={{ background: `conic-gradient(${buildGradient(rows, total)})` }}>
      <span><strong>{formatMoney(total, currency, 0)}</strong><small>{currency}</small></span>
    </div>
    <div className="approved-channel-legend">{rows.map(([channel, amount]) => {
      const visual = mediaVisual(channel)
      return <div key={channel}><span style={{ background: visual.color }} /><strong>{visual.label}</strong>
        <small>{formatMoney(amount, currency, 0)} ({Math.round(amount / total * 100)}%)</small></div>
    })}</div>
  </div><p className="approved-empty">{investmentDescription(currency, investment.otherCurrencies)}</p></>
}

function dashboardSignals(data: DashboardData, view: DashboardView): ExperienceSignal[] {
  const topChannel = view.investment.channels[0]
  const total = view.investment.totalMinor
  return [
    { label: 'Human decisions', value: `${data.tasks.length} shown`, icon: 'tasks',
      tone: data.tasks.length > 0 ? 'warning' : 'neutral', detail: homeCopy.tasksNote,
      why: 'Shown tasks belong to the selected workspace; open the queue for all available work.' },
    { label: 'Campaign movement', value: `${view.activeCampaigns.length} active`, icon: 'plan', tone: 'violet',
      detail: `${data.campaigns.length} campaigns exist in this workspace.`,
      why: 'Completed and cancelled campaigns are excluded from the active count.' },
    { label: 'Media concentration', value: total === null ? 'Prices incomplete' :
        topChannel && total > 0 ? mediaVisual(topChannel[0]).label : 'No confirmed value', icon: 'chart', tone: 'blue',
      detail: topChannel && total !== null && total > 0 ?
        `${Math.round(topChannel[1] / total * 100)}% of confirmed ${view.currency} media value is in this channel.` : homeCopy.confirmedSignalNote,
      why: homeCopy.confirmedSignalNote },
    { label: homeCopy.clientSignal, value: `${view.pendingDecisions} waiting`, icon: 'proposal',
      tone: view.pendingDecisions > 0 ? 'warning' : 'neutral', detail: homeCopy.decisionNote, why: homeCopy.clientSignalNote },
  ]
}

function Empty({ message }: { message: string }) { return <p className="approved-empty">{message}</p> }
function recentCampaigns(campaigns: Campaign[]) { return [...campaigns].sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc)).slice(0, 5) }
function relative(value: string) { const minutes = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 60000)); if (minutes < 60) return `${minutes}m ago`; const hours = Math.round(minutes / 60); if (hours < 24) return `${hours}h ago`; return `${Math.round(hours / 24)}d ago` }
function formatDateRange(start: string, end: string) { return `${new Date(start).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })} – ${new Date(end).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}` }
function buildGradient(rows: Array<[string, number]>, total: number) { let cursor = 0; return rows.map(([channel, amount]) => { const start = cursor; cursor += amount / total * 100; return `${mediaVisual(channel).color} ${start}% ${cursor}%` }).join(', ') }
function greeting() { const hour = new Date().getHours(); if (hour < 12) return 'Good morning'; if (hour < 18) return 'Good afternoon'; return 'Good evening' }
function firstName(displayName: string) { return displayName.trim().split(/\s+/)[0] || 'there' }
