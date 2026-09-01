import { useEffect, useMemo, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import type { HumanTask, Opportunity, Tenant, Workspace } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { bookingViewerRoles } from '../booking/booking-roles'
import { Icon, type IconName } from '../components/Icon'
import { marketplaceViewerRoles } from '../marketplace/marketplace-roles'
import { LoadingState, MessageState } from '../components/PageState'
import { formatDateTime, humanizeCode } from '../presentation/format'

type Counts = {
  clientAccounts: number | null
  agencies: number | null
  contacts: number | null
}

type DashboardData = {
  tenant: Tenant
  counts: Counts | null
  opportunities: Opportunity[] | null
  tasks: HumanTask[] | null
  limitedSections: number
}

type DashboardMetricData = {
  label: string
  value: number | null
  note: string
  icon: IconName
}

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
  return <HomeContent data={data} roleCode={workspace.roleCode} />
}

async function loadDashboard(tenantId: string): Promise<DashboardData> {
  const tenant = await api.getTenant(tenantId)
  const results = await Promise.allSettled([
    api.getFoundationCounts(tenantId),
    opportunityApi.list(tenantId),
    opportunityApi.listTasks(tenantId),
  ] as const)
  return {
    tenant,
    counts: fulfilled(results[0]),
    opportunities: fulfilled(results[1]),
    tasks: fulfilled(results[2]),
    limitedSections: results.filter(result => result.status === 'rejected').length,
  }
}

function fulfilled<T>(result: PromiseSettledResult<T>): T | null {
  return result.status === 'fulfilled' ? result.value : null
}

function buildMetrics(data: DashboardData): DashboardMetricData[] {
  return [
    { label: 'Active opportunities', value: data.opportunities?.length ?? null,
      note: 'Commercial work in progress', icon: 'target' },
    { label: 'Assigned actions', value: data.tasks?.length ?? null,
      note: 'Human decisions waiting', icon: 'tasks' },
    { label: 'Client accounts', value: data.counts?.clientAccounts ?? null,
      note: 'Visible in this workspace', icon: 'users' },
    { label: 'Contacts', value: data.counts?.contacts ?? null,
      note: 'Available to your role', icon: 'profile' },
  ]
}

function DashboardActions({ hasAssignedTask }: { hasAssignedTask: boolean }) {
  return <div className="dashboard-hero-actions">
    {hasAssignedTask && <Link className="primary-button" to="/tasks">
      Review assigned actions <Icon name="arrow" /></Link>}
    <Link className={hasAssignedTask ? 'secondary-button' : 'primary-button'}
      to="/briefs/new"><Icon name="plus" /> Understand a new Brief</Link>
  </div>
}

function HomeContent({ data, roleCode }: { data: DashboardData; roleCode: string }) {
  const { tenant, opportunities, tasks } = data
  const hasAssignedTask = Boolean(tasks?.length)
  const recentOpportunities = useMemo(() => [...(opportunities ?? [])]
    .sort((left, right) => right.updatedAtUtc.localeCompare(left.updatedAtUtc))
    .slice(0, 5), [opportunities])
  const metrics = buildMetrics(data)
  return <section className="work-dashboard" aria-labelledby="home-title">
    <header className="dashboard-command-header">
      <div><p className="eyebrow">Commercial operations</p>
        <h1 id="home-title">Work dashboard</h1>
        <p>Decisions, campaign activity and commercial work for {tenant.tradingName}.</p></div>
      <DashboardActions hasAssignedTask={hasAssignedTask} />
    </header>

    <dl className="dashboard-context-line" aria-label="Workspace market context">
      <div><dt>Workspace</dt><dd>{tenant.tradingName}</dd></div>
      <div><dt>Currency</dt><dd>{tenant.currencyCode}</dd></div>
      <div><dt>Time zone</dt><dd>{tenant.timeZone}</dd></div>
      <div><dt>Tax status</dt><dd>{humanizeCode(tenant.vatStatusCode, true)}</dd></div>
    </dl>

    {data.limitedSections > 0 && <p className="dashboard-access-note">
      Some work queues are not available to this role. Available information is shown below.
    </p>}

    <div className="dashboard-metrics" aria-label="Workspace summary">
      {metrics.map(metric => <DashboardMetric key={metric.label} {...metric} />)}
    </div>

    <div className="dashboard-main-grid">
      <WorkQueue tasks={tasks} />
      <OpportunityActivity opportunities={recentOpportunities} restricted={opportunities === null} />
    </div>

    <div className="dashboard-lower-grid">
      <StageDistribution opportunities={opportunities} />
      <QuickActions roleCode={roleCode} />
    </div>
  </section>
}

function DashboardMetric({ label, value, note, icon }: {
  label: string
  value: number | null
  note: string
  icon: IconName
}) {
  return <article className="dashboard-metric">
    <span className="dashboard-metric-icon"><Icon name={icon} /></span>
    <div><p>{label}</p><strong>{value ?? 'Restricted'}</strong><small>{value === null
      ? 'Not available to your role' : note}</small></div>
  </article>
}

function WorkQueue({ tasks }: { tasks: HumanTask[] | null }) {
  return <article className="workbench-panel">
    <header className="workbench-panel-heading"><div><p className="eyebrow">Your action queue</p>
      <h2>Decisions that need a person</h2><p>Only assigned, persisted checkpoints appear here.</p></div>
      {tasks && <span className="status-chip status-warning">{tasks.length} open</span>}</header>
    {tasks === null ? <RestrictedPanel /> : tasks.length === 0
      ? <div className="dashboard-empty"><span>✓</span><div><strong>You are clear</strong>
          <p>No decisions are currently assigned to you.</p></div></div>
      : <div className="dashboard-queue">{tasks.slice(0, 5).map(task => <Link key={task.id}
          to={task.briefId ? `/briefs/${task.briefId}` : `/opportunities/${task.opportunityId}`}>
          <span className="queue-mark"><Icon name="tasks" /></span><span><strong>{task.title}</strong>
            <small>{task.whyItMatters}</small></span><em>{humanizeCode(task.taskType, true)}</em>
        </Link>)}</div>}
    {tasks && tasks.length > 5 && <Link className="panel-footer-link" to="/tasks">
      View all assigned actions <Icon name="arrow" /></Link>}
  </article>
}

function OpportunityActivity({ opportunities, restricted }: {
  opportunities: Opportunity[]
  restricted: boolean
}) {
  return <article className="workbench-panel">
    <header className="workbench-panel-heading"><div><p className="eyebrow">Recent activity</p>
      <h2>Opportunity pipeline</h2><p>Latest persisted commercial work, ordered by change date.</p></div></header>
    {restricted ? <RestrictedPanel /> : opportunities.length === 0
      ? <div className="dashboard-empty"><span>→</span><div><strong>No opportunities yet</strong>
          <p>Start with a supplied Brief or create an evidence-led opportunity.</p></div></div>
      : <div className="opportunity-activity-list">{opportunities.map(item => <Link
          to={`/opportunities/${item.id}`} key={item.id}>
          <span><strong>{item.title}</strong><small>{item.objectiveSummary ??
            item.problemSummary ?? 'Open the opportunity to review its commercial context.'}</small></span>
          <span><em>{humanizeCode(item.stage, true)}</em><small>{formatDateTime(item.updatedAtUtc)}</small></span>
        </Link>)}</div>}
    {!restricted && <Link className="panel-footer-link" to="/opportunities">
      Open opportunities <Icon name="arrow" /></Link>}
  </article>
}

function StageDistribution({ opportunities }: { opportunities: Opportunity[] | null }) {
  const stageCounts = new Map<string, number>()
  for (const opportunity of opportunities ?? []) {
    stageCounts.set(opportunity.stage, (stageCounts.get(opportunity.stage) ?? 0) + 1)
  }
  const stages = [...stageCounts.entries()].sort((left, right) => right[1] - left[1])
  const maximum = Math.max(...stages.map(([, count]) => count), 1)
  return <article className="workbench-panel">
    <header className="workbench-panel-heading"><div><p className="eyebrow">Flow visibility</p>
      <h2>Where work is sitting</h2><p>Opportunity counts by current commercial stage.</p></div></header>
    {opportunities === null ? <RestrictedPanel /> : stages.length === 0
      ? <div className="dashboard-empty"><span>0</span><div><strong>No stage activity yet</strong>
          <p>The distribution will appear as real work enters the pipeline.</p></div></div>
      : <div className="stage-distribution">{stages.map(([stage, count]) => <div key={stage}>
          <span><strong>{humanizeCode(stage, true)}</strong><em>{count}</em></span>
          <span className="stage-distribution-track"><i style={{ width: `${count / maximum * 100}%` }} /></span>
        </div>)}</div>}
  </article>
}

function QuickActions({ roleCode }: { roleCode: string }) {
  const actions = availableQuickActions(roleCode)
  return <article className="workbench-panel quick-actions-panel">
    <header className="workbench-panel-heading"><div><p className="eyebrow">Start work</p>
      <h2>Common actions</h2><p>Enter at the point supported by the work you already have.</p></div></header>
    <div className="dashboard-quick-actions">{actions.map(action => <Link to={action.to} key={action.to}>
      <span><Icon name={action.icon} /></span><div><strong>{action.title}</strong><small>{action.copy}</small></div>
      <Icon name="arrow" /></Link>)}</div>
  </article>
}

type QuickAction = {
  to: string
  icon: IconName
  title: string
  copy: string
}

function availableQuickActions(roleCode: string): QuickAction[] {
  const actions: QuickAction[] = [
    { to: '/briefs/new', icon: 'brief', title: 'Understand a client Brief',
      copy: 'Paste the original request and let Advertified structure what is clear.' },
    { to: '/inventory', icon: 'inventory', title: 'Review media inventory',
      copy: 'Search published supply, evidence, rates and availability.' },
  ]
  if (marketplaceViewerRoles.has(roleCode)) actions.push({
    to: '/marketplace', icon: 'marketplace', title: 'Open supplier marketplace',
    copy: 'Discover published supplier snapshots and manage RFQs.',
  })
  if (bookingViewerRoles.has(roleCode)) actions.push({
    to: '/bookings', icon: 'reservation', title: 'Review bookings',
    copy: 'Move selected proposal lines through explicit supplier confirmation.',
  })
  return actions
}

function RestrictedPanel() {
  return <div className="dashboard-empty"><span>—</span><div><strong>Not available to this role</strong>
    <p>The rest of your workspace remains available.</p></div></div>
}
