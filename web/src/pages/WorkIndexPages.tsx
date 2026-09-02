import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { opportunityApi } from '../api/opportunity-client'
import { proposalApi } from '../api/proposal-client'
import type { ProposalSummary } from '../api/proposal-schemas'
import { planningApi } from '../api/planning-client'
import type { PlanningSummary } from '../api/planning-schemas'
import type { CampaignBriefSummary, HumanTask } from '../api/schemas'
import { useWorkspace } from '../auth/workspace-state'
import { Icon, type IconName } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, humanizeCode } from '../presentation/format'

type BriefListState = { briefs: CampaignBriefSummary[] }
type CampaignListState = { campaigns: Campaign[] }
type TaskListState = { tasks: HumanTask[] }
type ProposalListState = { proposals: ProposalSummary[] }
type PlanningListState = { planning: PlanningSummary[] }

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
    briefApi.list(tenantId),
    proposalApi.list(tenantId),
  ])
  return { briefs, proposals }
}
const loadTasks = async (tenantId: string): Promise<TaskListState> => ({
  tasks: await opportunityApi.listTasks(tenantId),
})
const loadCampaigns = async (tenantId: string): Promise<CampaignListState> => ({
  campaigns: await campaignApi.list(tenantId),
})

export function BriefsIndexPage() {
  return <BriefData>{briefs => <WorkspaceIndex title="Briefs"
    subtitle="Campaign requirements structured from the original client request."
    action={<Link className="primary-button" to="/briefs/new">+ New Brief</Link>}>
    {briefs.length === 0 ? <Empty label="No Briefs yet" /> : briefs.map(brief =>
      <IndexRow key={brief.id} icon="brief" title={brief.title} meta={`${brief.clientName} · ${humanizeCode(brief.status, true)}`}
        updated={brief.updatedAtUtc} to={`/briefs/${brief.id}`} />)}
  </WorkspaceIndex>}</BriefData>
}

export function StrategyStpIndexPage() {
  return <BriefData>{briefs => {
    const ready = briefs.filter(item => item.approvedVersionId || item.readyVersionId)
    return <WorkspaceIndex title="Strategy & STP"
      subtitle="Segmentation, targeting and positioning for approved campaign Briefs.">
      {ready.length === 0 ? <Empty label="No Brief is ready for Strategy & STP" /> : ready.map(brief => {
        const versionId = brief.approvedVersionId ?? brief.readyVersionId!
        return <IndexRow key={brief.id} icon="users" title={brief.title}
          meta={`${brief.clientName} · Brief ${humanizeCode(brief.status, true)}`}
          updated={brief.updatedAtUtc} to={`/stp/${versionId}`} />
      })}
    </WorkspaceIndex>
  }}</BriefData>
}

export function PlanningIndexPage() {
  return <PlanningData>{planning => <WorkspaceIndex title="Planning"
    subtitle="Media allocation, inventory selection and reconciled media plans.">
    {planning.length === 0 ? <Empty label="No campaign has reached media planning yet" /> : planning.map(item =>
      <IndexRow key={item.briefVersionId} icon="plan" title={item.briefTitle}
        meta={`${item.clientName} · STP ${humanizeCode(item.audienceStatus, true)} · Plan ${item.mediaPlanStatus ? humanizeCode(item.mediaPlanStatus, true) : 'Not created'}`}
        updated={item.updatedAtUtc} to={`/planning/${item.briefVersionId}`} />)}
  </WorkspaceIndex>}</PlanningData>
}

export function ProposalsIndexPage() {
  return <ProposalIndexData>{({ briefs, proposals }) => {
    const proposalBriefIds = new Set(proposals.map(item => item.briefId))
    const readyBriefs = briefs.filter(item => (item.approvedVersionId || item.readyVersionId) &&
      !proposalBriefIds.has(item.id))
    return <WorkspaceIndex title="Proposals"
      subtitle="Client proposals prepared from approved media plans.">
      {proposals.map(proposal => <IndexRow key={proposal.id} icon="proposal" title={proposal.title}
        meta={`Version ${proposal.versionNumber} · ${humanizeCode(proposal.status, true)}`}
        updated={proposal.createdAtUtc} to={`/proposals/${proposal.id}`} />)}
      {readyBriefs.map(brief => <IndexRow key={`brief-${brief.id}`} icon="brief" title={brief.title}
        meta={`${brief.clientName} · Ready to prepare proposal`} updated={brief.updatedAtUtc}
        to={`/briefs/${brief.id}/proposals/new`} />)}
      {proposals.length === 0 && readyBriefs.length === 0 && <Empty label="No proposal work is available yet" />}
    </WorkspaceIndex>
  }}</ProposalIndexData>
}

export function ApprovalsIndexPage() {
  return <TaskData>{tasks => {
    const approvals = tasks.filter(task => task.taskType.toUpperCase().includes('APPROVAL'))
    return <WorkspaceIndex title="Approvals" subtitle="Only decisions explicitly assigned for approval appear here.">
      {approvals.length === 0 ? <Empty label="No approvals are waiting" /> : approvals.map(task =>
        <IndexRow key={task.id} icon="shield" title={task.title} meta={task.whyItMatters}
          updated={task.createdAtUtc} to={taskRoute(task)} />)}
    </WorkspaceIndex>
  }}</TaskData>
}

export function MeasurementIndexPage() {
  return <CampaignData>{campaigns => <WorkspaceIndex title="Measurement"
    subtitle="Reviewed performance evidence and campaign measurement outputs.">
    {campaigns.length === 0 ? <Empty label="No campaign measurement is available yet" /> : campaigns.map(campaign =>
      <IndexRow key={campaign.id} icon="chart" title={campaign.title}
        meta={`${humanizeCode(campaign.status, true)} · ${campaign.performanceEvidence.length} evidence item(s) · ${campaign.measurementReports.length} report(s)`}
        updated={campaign.updatedAtUtc} to={`/campaigns/${campaign.id}#measurement`} />)}
  </WorkspaceIndex>}</CampaignData>
}

export function ReportsIndexPage() {
  return <CampaignData>{campaigns => {
    const reports = campaigns.flatMap(campaign => campaign.measurementReports.map(report => ({ campaign, report })))
    return <WorkspaceIndex title="Reports" subtitle="Campaign measurement reports built from reviewed evidence.">
      {reports.length === 0 ? <Empty label="No measurement reports have been generated yet" /> : reports.map(({ campaign, report }) =>
        <IndexRow key={report.id} icon="evidence" title={`${campaign.title} · Report ${report.versionNumber}`}
          meta={`${humanizeCode(report.status, true)} · ${report.evidence.length} evidence source(s)`}
          updated={report.updatedAtUtc} to={`/measurement-reports/${report.id}`} />)}
    </WorkspaceIndex>
  }}</CampaignData>
}

function WorkspaceIndex({ title, subtitle, action, children }: {
  title: string
  subtitle: string
  action?: ReactNode
  children: ReactNode
}) {
  return <section className="approved-work-index" aria-labelledby="work-index-title">
    <header className="approved-work-index-header"><div><h1 id="work-index-title">{title}</h1><p>{subtitle}</p></div>{action}</header>
    <div className="approved-work-index-list">{children}</div>
  </section>
}

function IndexRow({ icon, title, meta, updated, to }: {
  icon: IconName
  title: string
  meta: string
  updated: string
  to: string
}) {
  return <Link className="approved-work-index-row" to={to}><span><Icon name={icon} /></span>
    <div><strong>{title}</strong><small>{meta}</small></div><time>{formatDateTime(updated)}</time><Icon name="arrow" /></Link>
}

function Empty({ label }: { label: string }) {
  return <article className="approved-work-index-empty"><strong>{label}</strong><p>Advertified will show real persisted work here when it is available.</p></article>
}

function BriefData({ children }: { children: (briefs: CampaignBriefSummary[]) => ReactNode }) {
  const state = useTenantLoad(loadBriefs)
  if (state.loading) return <LoadingState label="Loading Briefs" />
  if (state.error || !state.value) return <MessageState title="Briefs could not be opened" message={state.error ?? 'Briefs are unavailable.'} />
  return <>{children(state.value.briefs)}</>
}

function PlanningData({ children }: { children: (planning: PlanningSummary[]) => ReactNode }) {
  const state = useTenantLoad(loadPlanning)
  if (state.loading) return <LoadingState label="Loading planning" />
  if (state.error || !state.value) return <MessageState title="Planning could not be opened" message={state.error ?? 'Planning is unavailable.'} />
  return <>{children(state.value.planning)}</>
}

function ProposalIndexData({ children }: { children: (value: BriefListState & ProposalListState) => ReactNode }) {
  const state = useTenantLoad(loadProposals)
  if (state.loading) return <LoadingState label="Loading proposals" />
  if (state.error || !state.value) return <MessageState title="Proposals could not be opened" message={state.error ?? 'Proposal work is unavailable.'} />
  return <>{children(state.value)}</>
}

function TaskData({ children }: { children: (tasks: HumanTask[]) => ReactNode }) {
  const state = useTenantLoad(loadTasks)
  if (state.loading) return <LoadingState label="Loading approvals" />
  if (state.error || !state.value) return <MessageState title="Approvals could not be opened" message={state.error ?? 'Approvals are unavailable.'} />
  return <>{children(state.value.tasks)}</>
}

function CampaignData({ children }: { children: (campaigns: Campaign[]) => ReactNode }) {
  const state = useTenantLoad(loadCampaigns)
  if (state.loading) return <LoadingState label="Loading campaign work" />
  if (state.error || !state.value) return <MessageState title="Campaign work could not be opened" message={state.error ?? 'Campaign work is unavailable.'} />
  return <>{children(state.value.campaigns)}</>
}

function useTenantLoad<T>(load: (tenantId: string) => Promise<T>) {
  const { selected, loading } = useWorkspace()
  const [value, setValue] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const tenantId = selected?.tenantId
  useEffect(() => {
    if (!tenantId) return
    let active = true
    void load(tenantId).then(result => { if (active) setValue(result) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, load])
  return useMemo(() => ({ selected, loading, value, error }), [selected, loading, value, error])
}

function taskRoute(task: HumanTask) {
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
