import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { MediaAllocation, MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { ExperienceSignals, type ExperienceSignal } from '../components/ExperienceSignals'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { ApprovedPlanningOverview } from '../planning/ApprovedPlanningOverview'
import { MediaMixEditor } from '../planning/MediaMixEditor'
import { MediaPlanPanel } from '../planning/MediaPlanPanel'
import { PlanningDecisionContext } from '../planning/PlanningDecisionContext'
import { MediaTimeline } from '../planning/MediaTimeline'
import { PlanningCommercialProof } from '../planning/PlanningCommercialProof'
import { PlanningOpportunityMap } from '../planning/PlanningOpportunityMap'
import { ShortlistPanel } from '../planning/ShortlistPanel'
import { announcePlanningChanged } from '../planning/planning-events'
import { mediaVisual } from '../planning/media-visuals'
import { humanizeCode } from '../presentation/format'
import { InventoryDecisionHistory } from '../reporting/InventoryDecisionHistory'

export function PlanningPage() {
  const briefVersionId = useParams().briefVersionId
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefVersionId || !session) return <Navigate to="/home" replace />
  return <PlanningWorkspaceRecord tenantId={selected.tenantId} briefVersionId={briefVersionId}
    token={session.antiforgeryToken} />
}

function PlanningWorkspaceRecord(props: PlanningContext) {
  const state = usePlanningWorkspace(props)
  if (state.error && !state.workspace) {
    return <MessageState title="Planning could not be opened" message={state.error} />
  }
  if (!state.workspace) return <LoadingState label="Loading media planning" />
  if (!state.workspace.campaignMode || !state.workspace.audience) {
    return <Navigate to={`/stp/${props.briefVersionId}`} replace />
  }
  return <><CampaignModeBinding mode={state.workspace.campaignMode.mode} />
    <PlanningWorkspaceContent {...props} workspace={state.workspace} busy={state.busy}
      error={state.error} act={state.act} /></>
}

type PlanningContext = { tenantId: string; briefVersionId: string; token: string }
type ActionRunner = (action: () => Promise<unknown>) => Promise<void>

function usePlanningWorkspace({ tenantId, briefVersionId }: PlanningContext) {
  const [workspace, setWorkspace] = useState<PlanningWorkspace | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const value = await planningApi.getWorkspace(tenantId, briefVersionId)
    setWorkspace(value); setError(null)
  }, [tenantId, briefVersionId])
  useEffect(() => {
    let active = true
    void planningApi.getWorkspace(tenantId, briefVersionId)
      .then(value => { if (active) setWorkspace(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, briefVersionId])
  const act: ActionRunner = async (action) => {
    setBusy(true); setError(null)
    try { await action(); await load(); announcePlanningChanged() }
    catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { workspace, error, busy, act }
}

function PlanningWorkspaceContent(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
}) {
  const { workspace } = props
  const mix = workspace.mediaMix
  const shortlist = currentShortlist(workspace, mix)
  const plan = currentPlan(workspace, mix)
  return <section aria-labelledby="planning-title" className="planning-page approved-media-planning-page">
    <Link className="text-action back-link" to={`/stp/${workspace.briefVersionId}`}>← Back to Audience Strategy</Link>
    <header className="approved-media-planning-header"><div><p className="eyebrow">Integrated plan across all selected channels</p>
      <h1 id="planning-title">Media Planning Overview</h1>
      <p>Allocate investment, select eligible supply and reconcile the client-ready media plan.</p></div>
      <span className="status-chip status-positive">{campaignModeLabel(workspace)}</span></header>
    {workspace.decisionContext && <PlanningDecisionContext value={workspace.decisionContext} />}
    <ExperienceSignals title="Planning intelligence" signals={planningSignals(workspace, mix, shortlist, plan)} />
    <PlanningCommercialProof workspace={workspace} mix={mix} shortlist={shortlist} plan={plan} />
    <PlanningOpportunityMap shortlist={shortlist} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {mix && <ApprovedPlanningOverview mix={mix} shortlist={shortlist} plan={plan} />}
    <PlanningStages {...props} mix={mix} shortlist={shortlist} plan={plan} />
    <InventoryDecisionHistory key={`${props.tenantId}-${props.briefVersionId}`}
      tenantId={props.tenantId} briefVersionId={props.briefVersionId} />
  </section>
}

function campaignModeLabel(workspace: PlanningWorkspace) {
  return workspace.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly
    ? 'Outdoor advertising and digital screens only' : 'Full campaign'
}

function PlanningStages(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
  mix: MediaMix | null; shortlist: Shortlist | null; plan: MediaPlan | null
}) {
  const mixApproved = isApproved(props.mix?.status)
  return <>
    <PlanningStagePanel number="1" title="Strategy & allocation" status={props.mix?.status ?? null}
      open={!mixApproved}><MixStage {...props} mix={props.mix} /></PlanningStagePanel>
    <SupplyPlanningStage {...props} enabled={mixApproved} />
    <CommercialPlanningStage {...props} enabled={isApproved(props.shortlist?.status)} />
  </>
}

function SupplyPlanningStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
  mix: MediaMix | null; shortlist: Shortlist | null; plan: MediaPlan | null; enabled: boolean
}) {
  if (!props.enabled) return null
  const approved = isApproved(props.shortlist?.status)
  return <PlanningStagePanel number="2" title="Supply & scenarios"
    status={props.shortlist?.status ?? null} open={!approved}>
    <ShortlistStage {...props} mix={props.mix} shortlist={props.shortlist} />
  </PlanningStagePanel>
}

function CommercialPlanningStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
  mix: MediaMix | null; shortlist: Shortlist | null; plan: MediaPlan | null; enabled: boolean
}) {
  if (!props.enabled) return null
  return <PlanningStagePanel number="3" title="Commercial reconciliation"
    status={props.plan?.status ?? null} open>
    <PlanStage {...props} shortlist={props.shortlist} plan={props.plan} />
  </PlanningStagePanel>
}

function isApproved(status: string | null | undefined) {
  return status === masterDataCodes.lifecycleStatuses.approved
}

function PlanningStagePanel({ number, title, status, open, children }: {
  number: string
  title: string
  status: string | null
  open: boolean
  children: ReactNode
}) {
  const [expanded, setExpanded] = useState(open)
  return <details className="planning-stage-panel" open={expanded}
    onToggle={event => setExpanded(event.currentTarget.open)}>
    <summary><span>{number}</span><div><strong>{title}</strong>
      <small>{status ? humanizeCode(status, true) : 'Not started'}</small></div><b>⌄</b></summary>
    <div className="planning-stage-content">{children}</div>
  </details>
}

function MixStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner; mix: MediaMix | null
}) {
  if (props.workspace.audience?.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!props.mix) return <StartCard eyebrow="Media allocation" title="Create the first media mix"
    copy="Start with a proposed allocation, then change the budgets, channel roles and independent running periods before confirming it."
    action="Create media mix" busy={props.busy} icon="chart"
    onAction={() => props.act(() => planningApi.generateMix(
      props.tenantId, props.briefVersionId, props.token))} />
  const mix = props.mix
  return <><MediaMixEditor key={`${mix.id}-${mix.version}`} mix={mix}
    purchaseCandidates={props.workspace.shortlist?.candidates ?? []}
    allowedChannels={props.workspace.campaignMode?.allowedChannels ?? []} busy={props.busy}
    onSave={(allocations: MediaAllocation[]) => props.act(() => planningApi.updateMix(
      props.tenantId, mix, allocations, props.token))}
    onApprove={() => props.act(() => planningApi.approveMix(props.tenantId, mix, props.token))}
    onRevise={() => props.act(() => planningApi.generateMix(
      props.tenantId, props.briefVersionId, props.token))} />
    {hasPeriods(mix) && <MediaTimeline allocations={mix.allocations} />}</>
}

function ShortlistStage(props: PlanningContext & {
  busy: boolean; act: ActionRunner; mix: MediaMix | null; shortlist: Shortlist | null
}) {
  if (props.mix?.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!props.shortlist) return <StartCard eyebrow="Supply selection" title="Find eligible inventory"
    copy="Apply the approved mix and hard Brief constraints to published inventory before scoring, benchmarking or supplier confirmation."
    action="Build inventory shortlist" busy={props.busy} icon="inventory"
    onAction={() => props.act(() => planningApi.generateShortlist(
      props.tenantId, props.briefVersionId, props.token))} />
  return <ShortlistPanel key={`${props.shortlist.id}-${props.shortlist.version}`}
    shortlist={props.shortlist}
    requiredChannels={props.mix.allocations
      .filter(item => item.budgetMinor > 0).map(item => item.channel)}
    busy={props.busy}
    onConfirm={(selectedIds, reason) => props.act(() => planningApi.selectShortlist(
      props.tenantId, props.shortlist!, selectedIds, props.token, reason))} />
}

function PlanStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner;
  shortlist: Shortlist | null; plan: MediaPlan | null
}) {
  if (props.shortlist?.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!props.plan) return <StartCard eyebrow="Commercial reconciliation" title="Create the media plan"
    copy="Price selected supply against each channel’s running periods and expose availability, freshness or benchmark objections before approval."
    action="Create media plan" busy={props.busy} icon="plan"
    onAction={() => props.act(() => planningApi.generatePlan(
      props.tenantId, props.briefVersionId, props.token))} />
  return <><MediaPlanPanel plan={props.plan} busy={props.busy}
    onResolve={(code) => props.act(() => planningApi.resolveObjection(
      props.tenantId, props.plan!, code, props.token))}
    onApprove={() => props.act(() => planningApi.approvePlan(
      props.tenantId, props.plan!, props.token))} />
    {props.plan.status === masterDataCodes.lifecycleStatuses.approved &&
      <article className="planning-start-card proposal-next-step"><div>
        <p className="eyebrow eyebrow-light">Client proposal</p>
        <h2>Turn approved plans into client choices</h2>
        <p>Select one to three materially different approved plans and prepare the branded proposal.</p>
      </div><Link className="primary-button" to={`/briefs/${props.workspace.briefId}/proposals/new`}>
        Prepare proposal <Icon name="arrow" />
      </Link></article>}
  </>
}

function StartCard({ eyebrow, title, copy, action, busy, icon, onAction }: {
  eyebrow: string; title: string; copy: string; action: string; busy: boolean;
  icon: 'users' | 'chart' | 'inventory' | 'plan'; onAction: () => Promise<void>
}) {
  return <article className="planning-start-card"><span className="planning-start-icon"><Icon name={icon} /></span>
    <div><p className="eyebrow eyebrow-light">{eyebrow}</p><h2>{title}</h2><p>{copy}</p></div>
    <button className="primary-button" type="button" disabled={busy}
      onClick={() => void onAction()}>{busy ? 'Working…' : action}</button></article>
}

function currentShortlist(workspace: PlanningWorkspace, mix: MediaMix | null): Shortlist | null {
  if (!mix || workspace.shortlist?.mixVersionId !== mix.id) return null
  return workspace.shortlist
}

function currentPlan(workspace: PlanningWorkspace, mix: MediaMix | null): MediaPlan | null {
  if (!mix || workspace.mediaPlan?.mixVersionId !== mix.id) return null
  return workspace.mediaPlan
}

function planningSignals(workspace: PlanningWorkspace, mix: MediaMix | null, shortlist: Shortlist | null, plan: MediaPlan | null): ExperienceSignal[] {
  return [campaignScopeSignal(workspace), allocationSignal(mix), supplySignal(shortlist), readinessSignal(plan)]
}

function campaignScopeSignal(workspace: PlanningWorkspace): ExperienceSignal {
  const oohOnly = workspace.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly
  return {
    label: 'Campaign scope', value: oohOnly ? 'Outdoor advertising and digital screens' : 'Full campaign', icon: 'target', tone: 'violet',
    detail: workspace.campaignMode?.isLocked ? 'The media scope is locked to this Brief lineage.' : 'The current media scope is not yet locked.',
    why: workspace.campaignMode?.reason || 'This mode is the persisted campaign-mode decision for the current Brief version.',
  }
}

function allocationSignal(mix: MediaMix | null): ExperienceSignal {
  const largest = largestAllocation(mix)
  if (!mix || !largest) return {
    label: 'Investment emphasis', value: 'Not allocated', icon: 'chart', tone: 'neutral',
    detail: 'Create the media mix to expose channel concentration.',
    why: 'No persisted media allocation exists yet.',
  }
  return {
    label: 'Investment emphasis', value: mediaVisual(largest.channel).label, icon: 'chart', tone: 'blue',
    detail: `${Math.round(largest.budgetMinor / Math.max(mix.totalBudgetMinor, 1) * 100)}% of the current mix is allocated here.`,
    why: largest.role || 'The signal follows the largest persisted channel allocation; it does not claim that the channel is optimal.',
  }
}

function largestAllocation(mix: MediaMix | null) {
  return mix?.allocations.reduce<MediaAllocation | null>((largest, item) =>
    !largest || item.budgetMinor > largest.budgetMinor ? item : largest, null) ?? null
}

function supplySignal(shortlist: Shortlist | null): ExperienceSignal {
  if (!shortlist) return {
    label: 'Supply selection', value: 'Not shortlisted', icon: 'inventory', tone: 'neutral',
    detail: 'Eligible supply follows approval of the media mix.', why: 'No shortlist exists for the current mix version yet.',
  }
  const selected = shortlist.candidates.filter(item => item.isSelected).length
  return {
    label: 'Supply selection', value: `${selected} selected`, icon: 'inventory', tone: selected > 0 ? 'positive' : 'neutral',
    detail: `${shortlist.candidates.length} candidate${shortlist.candidates.length === 1 ? '' : 's'} have been evaluated.`,
    why: 'Selected and rejected states come directly from the current shortlist version.',
  }
}

function readinessSignal(plan: MediaPlan | null): ExperienceSignal {
  if (!plan) return {
    label: 'Commercial readiness', value: 'Plan pending', icon: 'evidence', tone: 'neutral',
    detail: 'Commercial reconciliation starts after supply is confirmed.', why: 'No media plan exists for the current shortlist yet.',
  }
  const unresolved = plan.objections.filter(item => !item.resolution).length
  return {
    label: 'Commercial readiness', value: unresolved ? `${unresolved} objection${unresolved === 1 ? '' : 's'}` : 'Ready to review',
    icon: 'evidence', tone: unresolved ? 'warning' : 'positive',
    detail: `${plan.lines.length} priced plan line${plan.lines.length === 1 ? '' : 's'} are reconciled in the current version.`,
    why: 'Only unresolved persisted plan objections are counted here.',
  }
}

function hasPeriods(mix: MediaMix) {
  return mix.allocations.some(item => item.runningPeriods.length > 0)
}
