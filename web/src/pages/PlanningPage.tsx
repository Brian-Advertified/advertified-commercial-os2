import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { MediaAllocation, MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { MediaMixEditor } from '../planning/MediaMixEditor'
import { MediaPlanPanel } from '../planning/MediaPlanPanel'
import { MediaTimeline } from '../planning/MediaTimeline'
import { PlanningWorkbenchHeader } from '../planning/PlanningWorkbenchHeader'
import { ShortlistPanel } from '../planning/ShortlistPanel'
import { announcePlanningChanged } from '../planning/planning-events'
import { humanizeCode } from '../presentation/format'

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
  return <section aria-labelledby="planning-title" className="planning-page planning-workbench-page">
    <Link className="text-action back-link" to={`/briefs/${workspace.briefId}`}>← Back to Brief</Link>
    <PlanningWorkbenchHeader workspace={workspace} mix={mix} shortlist={shortlist} plan={plan} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <CampaignModeStage {...props} />
    <div id="audience-section"><AudienceStage {...props} /></div>
    <div id="media-mix"><MixStage {...props} mix={mix} /></div>
    <div id="inventory-selection"><ShortlistStage {...props} mix={mix} shortlist={shortlist} /></div>
    <div id="media-plan"><PlanStage {...props} shortlist={shortlist} plan={plan} /></div>
  </section>
}

function CampaignModeStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner
}) {
  const campaignMode = props.workspace.campaignMode
  if (campaignMode) return <section className="planning-mode-banner">
    <span className="planning-mode-icon"><Icon name={campaignMode.mode ===
      masterDataCodes.campaignModes.oohOnly ? 'inventory' : 'globe'} /></span>
    <div><p className="eyebrow">Campaign media scope</p>
      <h2>{campaignMode.mode === masterDataCodes.campaignModes.oohOnly
        ? 'OOH and DOOH only' : 'Full campaign'}</h2>
      <p>{campaignMode.mode === masterDataCodes.campaignModes.oohOnly
        ? 'The standard planning workflow is restricted to out-of-home channels.'
        : 'The standard planning workflow may use every channel configured for this market.'}</p></div>
    <dl><div><dt>Decision</dt><dd>{humanizeCode(campaignMode.decisionSource, true)}</dd></div>
      <div><dt>Confidence</dt><dd>{Math.round(campaignMode.confidence * 100)}%</dd></div>
      <div><dt>Change policy</dt><dd>Start a new campaign</dd></div></dl>
  </section>

  return <section className="planning-section campaign-mode-choice" aria-labelledby="campaign-mode-title">
    <div className="planning-section-heading"><div><p className="eyebrow">Media scope needs confirmation</p>
      <h2 id="campaign-mode-title">The Brief did not establish the campaign scope</h2>
      <p>Choose only because the available evidence was not strong enough for Advertified to decide. Both choices use the same audience, media mix, inventory, plan and proposal workflow.</p>
    </div></div>
    <div className="campaign-mode-options">
      <button type="button" disabled={props.busy} onClick={() => void props.act(() =>
        planningApi.selectCampaignMode(props.tenantId, props.briefVersionId,
          masterDataCodes.campaignModes.oohOnly, props.token, {
            source: masterDataCodes.campaignModeDecisionSources.humanClarification,
            confidence: 1,
            reason: 'The user resolved an unclear media requirement before planning.',
          }))}>
        <span className="campaign-mode-mark">OOH</span><strong>OOH and DOOH only</strong>
        <small>Restrict inventory and allocation to out-of-home channels.</small>
      </button>
      <button type="button" disabled={props.busy} onClick={() => void props.act(() =>
        planningApi.selectCampaignMode(props.tenantId, props.briefVersionId,
          masterDataCodes.campaignModes.fullCampaign, props.token, {
            source: masterDataCodes.campaignModeDecisionSources.humanClarification,
            confidence: 1,
            reason: 'The user resolved an unclear media requirement before planning.',
          }))}>
        <span className="campaign-mode-mark">360°</span><strong>Full campaign</strong>
        <small>Allow any media channel configured for the selected market.</small>
      </button>
    </div>
    <p className="campaign-mode-warning">This decision is locked once planning begins. A changed requirement starts a new campaign from the Brief.</p>
  </section>
}

function AudienceStage(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; act: ActionRunner
}) {
  if (!props.workspace.campaignMode) return null
  if (props.workspace.audience) return <AudienceSummary workspace={props.workspace} />
  return <StartCard eyebrow="Audience strategy" title="Define the audience direction"
    copy="Turn the approved Brief into evidence-labelled segments, targeting priorities and a positioning statement before allocating media."
    action="Build audience direction" busy={props.busy} icon="users"
    onAction={() => props.act(() => planningApi.generateAudiences(
      props.tenantId, props.briefVersionId, props.token))} />
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

function AudienceSummary({ workspace }: { workspace: PlanningWorkspace }) {
  const audience = workspace.audience!
  const targets = new Set(audience.targetAudienceIds)
  return <section className="planning-section audience-summary"><div className="planning-section-heading"><div>
    <p className="eyebrow">Segmentation, targeting and positioning</p>
    <h2>Audience strategy for this campaign</h2>
    <p>The same STP stage applies whether the campaign is OOH-only or permits every configured media channel.</p></div>
    <span className="status-chip status-positive">{humanizeCode(audience.status, true)}</span></div>
    <div className="stp-summary-grid">
      <article><span>Segmentation</span><strong>{audience.definitions.length} segment{audience.definitions.length === 1 ? '' : 's'} · {audience.targetAudienceIds.length} targeted</strong>
        <div className="audience-chip-grid">{audience.definitions.map(item => <div key={item.id}>
          <strong>{item.name}</strong><p>{item.description}</p><small>{targets.has(item.id) ? 'Target audience · ' : ''}{humanizeCode(item.classification, true)} · {Math.round(item.confidence * 100)}% confidence</small>
        </div>)}</div></article>
      <article><span>Targeting</span><strong>Who the plan must prioritise</strong><p>{audience.targetingRationale}</p></article>
      <article><span>Positioning</span><strong>What the campaign should establish</strong><p>{audience.positioningStatement}</p></article>
    </div></section>
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
