import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { MediaAllocation, MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { MediaMixEditor } from '../planning/MediaMixEditor'
import { MediaPlanPanel } from '../planning/MediaPlanPanel'
import { MediaTimeline } from '../planning/MediaTimeline'
import { ShortlistPanel } from '../planning/ShortlistPanel'
import { announcePlanningChanged } from '../planning/planning-events'
import { formatMoney, humanizeCode } from '../presentation/format'

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
    <Link className="text-action back-link" to={`/stp/${workspace.briefVersionId}`}>← Back to Strategy & STP</Link>
    <header className="approved-media-planning-header"><div><p className="eyebrow">Integrated plan across all selected channels</p>
      <h1 id="planning-title">Media Planning Overview</h1>
      <p>Allocate investment, select eligible supply and reconcile the client-ready media plan.</p></div>
      <span className="status-chip status-positive">{workspace.campaignMode?.mode === masterDataCodes.campaignModes.oohOnly ? 'OOH / DOOH only' : 'Full campaign'}</span></header>
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {mix && <ApprovedPlanningOverview mix={mix} plan={plan} />}
    <MixStage {...props} mix={mix} />
    <ShortlistStage {...props} mix={mix} shortlist={shortlist} />
    <PlanStage {...props} shortlist={shortlist} plan={plan} />
  </section>
}

function ApprovedPlanningOverview({ mix, plan }: { mix: MediaMix; plan: MediaPlan | null }) {
  const colors = ['#6538f5', '#2089ff', '#22bdd0', '#f5a524', '#ec6ba8', '#8f94a5']
  const total = Math.max(mix.totalBudgetMinor, 1)
  const selectedLines = plan?.lines ?? []
  const gradient = mix.allocations.map((item, index) => {
    const previous = mix.allocations.slice(0, index).reduce((sum, value) => sum + value.budgetMinor, 0)
    const start = previous / total * 100
    const end = (previous + item.budgetMinor) / total * 100
    return `${colors[index % colors.length]} ${start}% ${end}%`
  }).join(', ')
  return <section className="approved-planning-overview" aria-labelledby="approved-planning-overview-title">
    <header><div><p className="eyebrow">Media Planning Overview</p><h2 id="approved-planning-overview-title">Integrated plan across selected channels</h2></div>
      <span>{humanizeCode(mix.status, true)}</span></header>
    <div className="approved-planning-kpis">
      <PlanKpi label="Total Investment" value={formatMoney(mix.totalBudgetMinor, mix.currency, 0)} note="Planning budget" />
      <PlanKpi label="Total Reach" value="—" note="No verified reach forecast yet" />
      <PlanKpi label="Avg. Frequency" value="—" note="Requires verified forecast" />
      <PlanKpi label="Impressions" value="—" note="Requires verified forecast" />
    </div>
    <div className="approved-planning-visual-grid">
      <article className="approved-planning-investment"><header><h3>Investment by Channel</h3></header>
        <div><div className="approved-plan-donut" style={{ background: `conic-gradient(${gradient || '#edf0f4 0 100%'})` }}><span><strong>{formatMoney(mix.totalBudgetMinor, mix.currency, 0)}</strong><small>Total</small></span></div>
          <div className="approved-plan-legend">{mix.allocations.map((item, index) => <div key={item.channel}><i style={{ background: colors[index % colors.length] }} />
            <strong>{humanizeCode(item.channel, true)}</strong><span>{Math.round(item.budgetMinor / total * 100)}%</span><small>{formatMoney(item.budgetMinor, mix.currency, 0)}</small></div>)}</div></div></article>
      <article className="approved-media-flight"><header><h3>Media Flight</h3></header><div>{mix.allocations.map((item, index) => {
        const period = item.runningPeriods[0]
        return <div key={item.channel}><strong>{humanizeCode(item.channel, true)}</strong><span><i style={{ width: `${Math.max(18, 88 - index * 9)}%`, background: colors[index % colors.length] }} /></span>
          <small>{period ? `${period.start} → ${period.end}` : 'Dates not supplied'}</small></div>
      })}</div></article>
    </div>
    <article className="approved-top-placements"><header><h3>Top Placements</h3><span>{selectedLines.length} planned line{selectedLines.length === 1 ? '' : 's'}</span></header>
      {selectedLines.length === 0 ? <p className="approved-empty">Top placements will appear after inventory is selected and the plan is created.</p> : <div className="approved-placement-table"><div><span>Channel</span><span>Placement</span><span>Location</span><span>Flight</span><span>Investment</span></div>
        {selectedLines.slice(0, 7).map(line => <div key={line.id}><strong>{humanizeCode(line.channel, true)}</strong><span>{line.name}</span><span>{line.geography}</span>
          <span>{line.runningPeriods[0] ? `${line.runningPeriods[0].start} – ${line.runningPeriods[0].end}` : '—'}</span><strong>{formatMoney(line.clientPriceMinor, plan!.currency, 0)}</strong></div>)}</div>}
    </article>
  </section>
}

function PlanKpi({ label, value, note }: { label: string; value: string; note: string }) {
  return <article><span>{label}</span><strong>{value}</strong><small>{note}</small></article>
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
    shortlist={props.shortlist} busy={props.busy}
    onConfirm={(selectedIds) => props.act(() => planningApi.selectShortlist(
      props.tenantId, props.shortlist!, selectedIds, props.token))} />
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

function hasPeriods(mix: MediaMix) {
  return mix.allocations.some(item => item.runningPeriods.length > 0)
}
