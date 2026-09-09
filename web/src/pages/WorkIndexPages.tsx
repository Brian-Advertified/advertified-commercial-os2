import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
export { MeasurementIndexPage, ReportsIndexPage } from './MeasurementIndexPages'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { proposalApi } from '../api/proposal-client'
import type { ProposalSummary } from '../api/proposal-schemas'
import { planningApi } from '../api/planning-client'
import type { PlanningSummary } from '../api/planning-schemas'
import type { CampaignBriefSummary, HumanTask } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { BriefsWorkspace } from '../brief/BriefsWorkspace'
import { Icon, type IconName } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'

type BriefListState = { briefs: CampaignBriefSummary[] }
type TaskListState = { tasks: HumanTask[] }
type ProposalListState = { proposals: ProposalSummary[] }
type PlanningListState = { planning: PlanningSummary[] }
type QueueMetric = { label: string; value: number | string; note: string }

const loadBriefs = async (tenantId: string): Promise<BriefListState> => ({
  briefs: await briefApi.list(tenantId),
})
const loadPlanning = async (tenantId: string): Promise<PlanningListState> => ({
  planning: await planningApi.list(tenantId),
})
const loadProposals = async (
  tenantId: string,
): Promise<BriefListState & ProposalListState> => {
  const [briefs, proposals] = await Promise.all([
    briefApi.list(tenantId), proposalApi.list(tenantId),
  ])
  return { briefs, proposals }
}
const loadTasks = async (tenantId: string): Promise<TaskListState> => ({
  tasks: await opportunityApi.listTasks(tenantId),
})

export function BriefsIndexPage() {
  return <BriefData>{briefs => <BriefsWorkspace briefs={briefs} />}</BriefData>
}

export function StrategyStpIndexPage() {
  return <BriefData>{briefs => {
    const ready = briefs.filter(item => item.approvedVersionId || item.readyVersionId)
    const summary = [{ label: 'Ready Briefs', value: ready.length,
      note: 'Approved or review-ready campaign Briefs' }]
    return <WorkspaceIndex title="Audience Strategy"
      subtitle="Prioritise the audiences that should guide each approved campaign Brief."
      summary={summary}>
      {ready.length === 0 ? <Empty label="No Brief is ready for Audience Strategy"
        action="Approve or complete a Brief to start audience discovery." /> : ready.map(brief => {
        const versionId = brief.approvedVersionId ?? brief.readyVersionId!
        return <IndexRow key={brief.id} icon="users" title={brief.title}
          meta={`${brief.clientName} · Brief ${humanizeCode(brief.status, true)}`}
          state="Audience decision" nextAction="Review audience strategy"
          updated={brief.updatedAtUtc} to={`/stp/${versionId}`} />
      })}
    </WorkspaceIndex>
  }}</BriefData>
}

export function PlanningIndexPage() {
  return <PlanningData>{planning => {
    const approved = planning.filter(item =>
      item.mediaPlanStatus === masterDataCodes.lifecycleStatuses.approved).length
    const audiencePending = planning.filter(item =>
      item.audienceStatus !== masterDataCodes.lifecycleStatuses.approved).length
    const summary: QueueMetric[] = [
      { label: 'In planning', value: planning.length, note: 'Campaign Briefs in this queue' },
      { label: 'Audience pending', value: audiencePending, note: 'Need audience approval first' },
      { label: 'Plan approved', value: approved, note: 'Ready to progress to proposal' },
    ]
    return <WorkspaceIndex title="Planning"
      subtitle="Move each campaign from approved audience to media allocation, supply and a reconciled plan."
      summary={summary}>
      {planning.length === 0 ? <Empty label="No campaign has reached media planning yet"
        action="Approve an Audience Strategy to unlock media planning." /> : planning.map(item => {
        const stage = planningStage(item)
        return <IndexRow key={item.briefVersionId} icon="plan" title={item.briefTitle}
          meta={`${item.clientName} · ${stage.detail}`}
          state={stage.state} nextAction={stage.nextAction}
          updated={item.updatedAtUtc} to={`/planning/${item.briefVersionId}`} />
      })}
    </WorkspaceIndex>
  }}</PlanningData>
}

export function ProposalsIndexPage() {
  return <ProposalIndexData>{({ briefs, proposals }) => {
    const proposalBriefIds = new Set(proposals.map(item => item.briefId))
    const readyBriefs = briefs.filter(item => (item.approvedVersionId || item.readyVersionId) &&
      !proposalBriefIds.has(item.id))
    const summary: QueueMetric[] = [
      { label: 'Proposals', value: proposals.length, note: 'Persisted proposal records' },
      { label: 'Ready to prepare', value: readyBriefs.length, note: 'Briefs without a proposal yet' },
    ]
    return <WorkspaceIndex title="Proposals"
      subtitle="Turn approved media plans into clear client choices and track the decision handoff."
      summary={summary}>
      {proposals.map(proposal => <IndexRow key={proposal.id} icon="proposal" title={proposal.title}
        meta={`Version ${proposal.versionNumber} · ${humanizeCode(proposal.status, true)}`}
        state={humanizeCode(proposal.status, true)} nextAction="Continue proposal"
        updated={proposal.createdAtUtc} to={`/proposals/${proposal.id}`} />)}
      {readyBriefs.map(brief => <IndexRow key={`brief-${brief.id}`} icon="brief" title={brief.title}
        meta={`${brief.clientName} · Approved planning available`}
        state="Plan ready" nextAction="Prepare client proposal" updated={brief.updatedAtUtc}
        to={`/briefs/${brief.id}/proposals/new`} />)}
      {proposals.length === 0 && readyBriefs.length === 0 && <Empty
        label="No proposal work is available yet"
        action="Approve a media plan before preparing client choices." />}
    </WorkspaceIndex>
  }}</ProposalIndexData>
}

export function ApprovalsIndexPage() {
  return <TaskData>{tasks => {
    const approvals = tasks.filter(task => task.taskType.toUpperCase().includes('APPROVAL'))
    return <WorkspaceIndex title="Approvals"
      subtitle="Only decisions explicitly assigned for approval appear here."
      summary={[{ label: 'Waiting', value: approvals.length, note: 'Assigned approval decisions' }]}>
      {approvals.length === 0 ? <Empty label="No approvals are waiting"
        action="Assigned approval work will appear here." /> : approvals.map(task =>
        <IndexRow key={task.id} icon="shield" title={task.title} meta={task.whyItMatters}
          state="Assigned approval" nextAction="Review decision"
          updated={task.createdAtUtc} to={taskRoute(task)} />)}
    </WorkspaceIndex>
  }}</TaskData>
}

function WorkspaceIndex({ title, subtitle, action, summary = [], children }: {
  title: string
  subtitle: string
  action?: ReactNode
  summary?: QueueMetric[]
  children: ReactNode
}) {
  return <section className="approved-work-index" aria-labelledby="work-index-title">
    <header className="approved-work-index-header"><div><h1 id="work-index-title">{title}</h1>
      <p>{subtitle}</p></div>{action}</header>
    {summary.length > 0 && <div className="approved-work-queue-summary">{summary.map(item =>
      <article key={item.label}><span>{item.label}</span><strong>{item.value}</strong>
        <small>{item.note}</small></article>)}</div>}
    <div className="approved-work-index-list">{children}</div>
  </section>
}

function IndexRow({ icon, title, meta, state, nextAction, updated, to }: {
  icon: IconName
  title: string
  meta: string
  state?: string
  nextAction?: string
  updated: string
  to: string
}) {
  return <Link className="approved-work-index-row" to={to}><span><Icon name={icon} /></span>
    <div><strong>{title}</strong><small>{meta}</small>
      {(state || nextAction) && <em>{state}{state && nextAction ? ' · ' : ''}{nextAction}</em>}</div>
    <time>{formatDateTime(updated)}</time><Icon name="arrow" /></Link>
}

function Empty({ label, action }: { label: string; action: string }) {
  return <article className="approved-work-index-empty"><strong>{label}</strong><p>{action}</p></article>
}

function planningStage(item: PlanningSummary) {
  if (item.audienceStatus !== masterDataCodes.lifecycleStatuses.approved) return {
    state: 'Audience pending', nextAction: 'Approve audience strategy',
    detail: `Audience ${humanizeCode(item.audienceStatus, true)}`,
  }
  if (!item.mediaMixStatus) return {
    state: 'Allocation', nextAction: 'Create media mix', detail: 'Audience approved · Mix not created',
  }
  if (item.mediaMixStatus !== masterDataCodes.lifecycleStatuses.approved) return {
    state: 'Allocation', nextAction: 'Review media mix',
    detail: `Mix ${humanizeCode(item.mediaMixStatus, true)}`,
  }
  if (!item.mediaPlanStatus) return {
    state: 'Supply selection', nextAction: 'Build shortlist and media plan',
    detail: 'Mix approved · Plan not created',
  }
  if (item.mediaPlanStatus !== masterDataCodes.lifecycleStatuses.approved) return {
    state: 'Commercial review', nextAction: 'Review media plan',
    detail: `Plan ${humanizeCode(item.mediaPlanStatus, true)}`,
  }
  return { state: 'Plan approved', nextAction: 'Prepare proposal', detail: 'Approved media plan' }
}

function BriefData({ children }: { children: (briefs: CampaignBriefSummary[]) => ReactNode }) {
  const state = useTenantLoad(loadBriefs)
  if (!state.selected && !state.loading) return <Navigate to="/workspaces" replace />
  if (state.loading) return <LoadingState label="Loading Briefs" />
  if (state.error || !state.value) return <MessageState title="Briefs could not be opened"
    message={state.error ?? 'Briefs are unavailable.'} />
  return <>{children(state.value.briefs)}</>
}

function PlanningData({ children }: { children: (planning: PlanningSummary[]) => ReactNode }) {
  const state = useTenantLoad(loadPlanning)
  if (!state.selected && !state.loading) return <Navigate to="/workspaces" replace />
  if (state.loading) return <LoadingState label="Loading planning" />
  if (state.error || !state.value) return <MessageState title="Planning could not be opened"
    message={state.error ?? 'Planning is unavailable.'} />
  return <>{children(state.value.planning)}</>
}

function ProposalIndexData({ children }: {
  children: (value: BriefListState & ProposalListState) => ReactNode
}) {
  const state = useTenantLoad(loadProposals)
  if (!state.selected && !state.loading) return <Navigate to="/workspaces" replace />
  if (state.loading) return <LoadingState label="Loading proposals" />
  if (state.error || !state.value) return <MessageState title="Proposals could not be opened"
    message={state.error ?? 'Proposal work is unavailable.'} />
  return <>{children(state.value)}</>
}

function TaskData({ children }: { children: (tasks: HumanTask[]) => ReactNode }) {
  const state = useTenantLoad(loadTasks)
  if (!state.selected && !state.loading) return <Navigate to="/workspaces" replace />
  if (state.loading) return <LoadingState label="Loading approvals" />
  if (state.error || !state.value) return <MessageState title="Approvals could not be opened"
    message={state.error ?? 'Approvals are unavailable.'} />
  return <>{children(state.value.tasks)}</>
}

function useTenantLoad<T>(load: (tenantId: string) => Promise<T>) {
  const { selected, loading: workspaceLoading } = useWorkspace()
  const [value, setValue] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loadedTenantId, setLoadedTenantId] = useState<string | undefined>()
  const tenantId = selected?.tenantId
  useEffect(() => {
    if (!tenantId) return
    let active = true
    void load(tenantId).then(result => {
      if (active) {
        setValue(result); setError(null); setLoadedTenantId(tenantId)
      }
    }).catch((failure: unknown) => {
      if (active) {
        setValue(null); setError(humanMessage(failure)); setLoadedTenantId(tenantId)
      }
    })
    return () => { active = false }
  }, [tenantId, load])
  return useMemo(
    () => ({ selected,
      loading: workspaceLoading || Boolean(tenantId && loadedTenantId !== tenantId),
      value: loadedTenantId === tenantId ? value : null,
      error: loadedTenantId === tenantId ? error : null }),
    [selected, workspaceLoading, loadedTenantId, tenantId, value, error],
  )
}

function taskRoute(task: HumanTask) {
  if (task.resourceType === masterDataCodes.commercialResourceTypes.inventoryImport)
    return `/inventory/imports/${task.resourceId}`
  if (task.resourceType.toLowerCase().includes('proposal')) return `/proposals/${task.resourceId}`
  if (task.resourceType.toLowerCase().includes(
    masterDataCodes.commercialResourceTypes.strategy,
  )) return `/strategies/${task.resourceId}`
  if (task.briefId) return `/briefs/${task.briefId}`
  if (task.opportunityId) return `/opportunities/${task.opportunityId}`
  return '/tasks'
}

export function RequireWorkspace({ children }: { children: ReactNode }) {
  const { selected, loading } = useWorkspace()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  return <>{children}</>
}
