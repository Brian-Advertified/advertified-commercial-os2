import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { MediaAllocation, MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { MediaMixEditor } from '../planning/MediaMixEditor'
import { MediaPlanPanel } from '../planning/MediaPlanPanel'
import { MediaTimeline } from '../planning/MediaTimeline'
import { ShortlistPanel } from '../planning/ShortlistPanel'
import { announcePlanningChanged } from '../planning/planning-events'

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
  return <PlanningWorkspaceContent {...props} workspace={state.workspace} busy={state.busy}
    error={state.error} act={state.act} />
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
  return <section aria-labelledby="planning-title" className="planning-page">
    <Link className="text-action back-link" to="/home">← Back to work</Link>
    <PlanningHero workspace={workspace} mix={mix} shortlist={shortlist} plan={plan} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <CampaignModeStage {...props} />
    <AudienceStage {...props} />
    <MixStage {...props} mix={mix} />
    <ShortlistStage {...props} mix={mix} shortlist={shortlist} />
    <PlanStage {...props} shortlist={shortlist} plan={plan} />
  </section>
}

function CampaignModeStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner
}) {
  const campaignMode = props.workspace.campaignMode
  if (campaignMode) return <section className="planning-section campaign-mode-locked">
    <div><p className="eyebrow">Campaign planning</p>
      <h2>{campaignMode.mode === masterDataCodes.campaignModes.oohOnly
        ? 'Out-of-home only' : 'Full campaign'}</h2>
      <p>{campaignMode.mode === masterDataCodes.campaignModes.oohOnly
        ? 'This campaign follows the standard planning workflow with only out-of-home media available.'
        : 'This campaign follows the standard planning workflow with all selected media types available.'}</p></div>
    <div className="campaign-mode-lock-note"><strong>Selection locked</strong>
      <span>Changing campaign type requires a new campaign and a fresh planning process.</span>
      <Link className="text-action" to="/briefs/new">Start a new campaign</Link></div>
  </section>

  return <section className="planning-section campaign-mode-choice" aria-labelledby="campaign-mode-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Campaign planning</p>
      <h2 id="campaign-mode-title">Choose the media selection</h2>
      <p>Both choices use the same audience, media mix, inventory, plan and proposal workflow. The only difference is whether out-of-home is the only selected media.</p>
    </div></div>
    <div className="campaign-mode-options">
      <button type="button" disabled={props.busy} onClick={() => void props.act(() =>
        planningApi.selectCampaignMode(props.tenantId, props.briefVersionId,
          masterDataCodes.campaignModes.oohOnly, props.token))}>
        <span className="campaign-mode-mark">OOH</span><strong>Out-of-home only</strong>
        <small>Use OOH and digital OOH, with the same planning and approval stages.</small>
      </button>
      <button type="button" disabled={props.busy} onClick={() => void props.act(() =>
        planningApi.selectCampaignMode(props.tenantId, props.briefVersionId,
          masterDataCodes.campaignModes.fullCampaign, props.token))}>
        <span className="campaign-mode-mark">360°</span><strong>Full campaign</strong>
        <small>Use out-of-home alongside one or more other selected media types.</small>
      </button>
    </div>
    <p className="campaign-mode-warning">This choice cannot be changed later. Moving from out-of-home only to a full campaign requires a new campaign and a fresh start.</p>
  </section>
}

function AudienceStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner
}) {
  if (!props.workspace.campaignMode) return null
  if (props.workspace.audience) {
    return <AudienceSummary workspace={props.workspace} />
  }
  return <StartCard title="Define the audience direction"
    copy="Turn the approved Brief into evidence-labelled audience definitions before allocating media."
    action="Build audience direction" busy={props.busy}
    onAction={() => props.act(() => planningApi.generateAudiences(
      props.tenantId, props.briefVersionId, props.token))} />
}

function MixStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner; mix: MediaMix | null
}) {
  if (props.workspace.audience?.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!props.mix) return <StartCard title="Create the first media mix"
    copy="Start with a proposed allocation, then change the budgets, roles and running periods before confirming it."
    action="Create media mix" busy={props.busy}
    onAction={() => props.act(() => planningApi.generateMix(
      props.tenantId, props.briefVersionId, props.token))} />
  const mix = props.mix
  return <><MediaMixEditor key={`${mix.id}-${mix.version}`} mix={mix} busy={props.busy}
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
  if (!props.shortlist) return <StartCard title="Find eligible inventory"
    copy="Apply the approved mix and hard Brief constraints to published inventory before scoring or benchmarking."
    action="Build inventory shortlist" busy={props.busy}
    onAction={() => props.act(() => planningApi.generateShortlist(
      props.tenantId, props.briefVersionId, props.token))} />
  const shortlist = props.shortlist
  return <ShortlistPanel key={`${shortlist.id}-${shortlist.version}`} shortlist={shortlist} busy={props.busy}
    onConfirm={(selectedIds) => props.act(() => planningApi.selectShortlist(
      props.tenantId, shortlist, selectedIds, props.token))} />
}

function PlanStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner;
  shortlist: Shortlist | null; plan: MediaPlan | null
}) {
  if (props.shortlist?.status !== masterDataCodes.lifecycleStatuses.approved) return null
  if (!props.plan) return <StartCard title="Reconcile the media plan"
    copy="Price selected supply against each channel’s running periods and expose supply or benchmark objections."
    action="Create media plan" busy={props.busy}
    onAction={() => props.act(() => planningApi.generatePlan(
      props.tenantId, props.briefVersionId, props.token))} />
  const plan = props.plan
  return <><MediaPlanPanel plan={plan} busy={props.busy}
    onResolve={(code) => props.act(() => planningApi.resolveObjection(
      props.tenantId, plan, code, props.token))}
    onApprove={() => props.act(() => planningApi.approvePlan(props.tenantId, plan, props.token))} />
    {plan.status === masterDataCodes.lifecycleStatuses.approved &&
      <article className="planning-start-card proposal-next-step"><div>
        <p className="eyebrow eyebrow-light">Client proposal</p>
        <h2>Turn approved plans into client choices</h2>
        <p>Select up to three genuinely different approved plans, refine the outcomes and prepare the branded proposal.</p>
      </div><Link className="primary-button" to={`/briefs/${props.workspace.briefId}/proposals/new`}>
        Prepare proposal
      </Link></article>}
  </>
}

function PlanningHero({ workspace, mix, shortlist, plan }: {
  workspace: PlanningWorkspace; mix: MediaMix | null; shortlist: Shortlist | null; plan: MediaPlan | null
}) {
  const steps = [
    ['Media choice', Boolean(workspace.campaignMode)],
    ['Audience', Boolean(workspace.audience)],
    ['Media mix', mix?.status === masterDataCodes.lifecycleStatuses.approved],
    ['Inventory', shortlist?.status === masterDataCodes.lifecycleStatuses.approved],
    ['Plan', plan?.status === masterDataCodes.lifecycleStatuses.approved],
  ] as const
  return <header className="planning-hero"><div><p className="eyebrow eyebrow-light">Media planning</p>
    <h1 id="planning-title">Build a plan you can change</h1>
    <p>Balance the media investment, schedule each channel independently, compare eligible supply and approve only when the plan is commercially reconciled.</p></div>
    <div className="planning-progress" aria-label="Planning progress">{steps.map(([label, complete], index) =>
      <div key={label} className={complete ? 'is-complete' : ''}><span>{complete ? '✓' : index + 1}</span>{label}</div>)}</div></header>
}

function AudienceSummary({ workspace }: { workspace: PlanningWorkspace }) {
  const audience = workspace.audience!
  const targets = new Set(audience.targetAudienceIds)
  return <section className="planning-section audience-summary"><div className="planning-section-heading"><div>
    <p className="eyebrow">Segmentation, targeting and positioning</p>
    <h2>Audience strategy for this campaign</h2>
    <p>The same STP stage applies whether the selected media is OOH-only or a full campaign.</p></div>
    <span className="status-chip">{audience.status}</span></div>
    <div className="stp-summary-grid">
      <article><span>Segmentation</span><strong>{audience.definitions.length} segment{audience.definitions.length === 1 ? '' : 's'} · {audience.targetAudienceIds.length} targeted</strong>
        <div className="audience-chip-grid">{audience.definitions.map(item => <div key={item.id}>
          <strong>{item.name}</strong><p>{item.description}</p><small>{targets.has(item.id) ? 'Target audience · ' : ''}{item.classification.replaceAll('_', ' ')} · {Math.round(item.confidence * 100)}% confidence</small>
        </div>)}</div></article>
      <article><span>Targeting</span><strong>Who the plan must prioritise</strong><p>{audience.targetingRationale}</p></article>
      <article><span>Positioning</span><strong>What the campaign should establish</strong><p>{audience.positioningStatement}</p></article>
    </div></section>
}

function StartCard({ title, copy, action, busy, onAction }: {
  title: string; copy: string; action: string; busy: boolean; onAction: () => Promise<void>
}) {
  return <article className="planning-start-card"><div><p className="eyebrow eyebrow-light">Next decision</p>
    <h2>{title}</h2><p>{copy}</p></div><button className="primary-button" type="button"
      disabled={busy} onClick={() => void onAction()}>{busy ? 'Working…' : action}</button></article>
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
