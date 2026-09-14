import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useLocation, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { MediaAllocation, MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'
import { ConnectedStrategyView } from '../planning/ConnectedStrategyView'
import { FlightEditorDrawer, ReplacementDrawer } from '../planning/PlanningDrawers'
import { MediaMixEditor } from '../planning/MediaMixEditor'
import { MediaPlanRow } from '../planning/MediaPlanRow'
import { MediaPlanPanel } from '../planning/MediaPlanPanel'
import { PlanningDecisionContext } from '../planning/PlanningDecisionContext'
import { PlanningMeasurementReadiness } from '../planning/PlanningMeasurementReadiness'
import { PlanningOpportunityMap } from '../planning/PlanningOpportunityMap'
import { ShortlistPanel } from '../planning/ShortlistPanel'
import { isApproved } from '../planning/planning-presentation'
import { announcePlanningChanged } from '../planning/planning-events'
import { mediaVisual } from '../planning/media-visuals'
import { formatMoney } from '../presentation/format'
import { InventoryDecisionHistory } from '../reporting/InventoryDecisionHistory'

export function PlanningPage() {
  const briefVersionId = useParams().briefVersionId
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefVersionId || !session) return <Navigate to="/home" replace />
  return <PlanningWorkspaceRecord key={`${selected.tenantId}:${briefVersionId}`}
    tenantId={selected.tenantId} briefVersionId={briefVersionId} token={session.antiforgeryToken} />
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
    catch (failure) {
      const message = humanMessage(failure)
      try { setWorkspace(await planningApi.getWorkspace(tenantId, briefVersionId)) }
      catch { /* Preserve the actionable command failure when recovery loading is unavailable. */ }
      setError(message); notifications.failure(message)
    }
    finally { setBusy(false) }
  }
  return { workspace, error, busy, act }
}

function PlanningWorkspaceContent(props: PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
}) {
  const location = useLocation()
  const { workspace } = props
  const mix = workspace.mediaMix
  const shortlist = currentShortlist(workspace, mix)
  const plan = currentPlan(workspace, mix)
  if (location.hash === '#strategy') {
    return <ConnectedStrategyView {...props} mix={mix} />
  }
  return <ConnectedMediaPlanningView {...props} mix={mix} shortlist={shortlist} plan={plan} />
}

type MediaPlanningProps = PlanningContext & {
  workspace: PlanningWorkspace; busy: boolean; error: string | null; act: ActionRunner
  mix: MediaMix | null; shortlist: Shortlist | null; plan: MediaPlan | null
}

function ConnectedMediaPlanningView(props: MediaPlanningProps) {
  const [flightChannel, setFlightChannel] = useState<string | null>(null)
  const [replaceCandidateId, setReplaceCandidateId] = useState<string | null>(null)
  const [activeChannel, setActiveChannel] = useState<string | null>(null)
  if (!props.mix) return <Navigate to={`/planning/${props.workspace.briefVersionId}#strategy`} replace />
  return <MediaPlanningWorkspace {...props} mix={props.mix} flightChannel={flightChannel}
    replaceCandidateId={replaceCandidateId} activeChannel={activeChannel}
    setFlightChannel={setFlightChannel} setReplaceCandidateId={setReplaceCandidateId}
    setActiveChannel={setActiveChannel} />
}

function MediaPlanningWorkspace(props: MediaPlanningProps & {
  mix: MediaMix
  flightChannel: string | null
  replaceCandidateId: string | null
  activeChannel: string | null
  setFlightChannel: (value: string | null) => void
  setReplaceCandidateId: (value: string | null) => void
  setActiveChannel: (value: string | null) => void
}) {
  const editable = props.mix.status === masterDataCodes.lifecycleStatuses.draft
  const selected = selectedCandidates(props.shortlist)
  return <section className={`connected-media-plan-page${editable ? ' is-edit-mode' : ''}`}>
    <MediaPlanningHeading replacing={Boolean(props.replaceCandidateId)} />
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    <MediaPlanEditBanner {...props} editable={editable} />
    <ChannelFilterbar mix={props.mix} active={props.activeChannel} onChange={props.setActiveChannel} />
    <div className="connected-media-plan-grid">
      <MediaPlanMain {...props} selected={selected} editable={editable} activeChannel={props.activeChannel} />
      <MediaPlanSummary mix={props.mix} shortlist={props.shortlist} selected={selected} />
    </div>
    <PlanningContextPanels {...props} />
    <MediaPlanDrawers {...props} selected={selected} editable={editable} />
  </section>
}

function MediaPlanningHeading({ replacing }: { replacing: boolean }) {
  return <header className="connected-stage-heading"><div><p className="eyebrow">Campaign planning</p>
    <h1>{replacing ? 'Replace Inventory / Alternatives' : 'Media plan & partner selection'}</h1>
    <p>{replacing
      ? 'Swap a placement with the best eligible alternative without losing campaign context or decision history.'
      : 'Plan, compare and select the best mix of media channels and eligible partners for your campaign.'}</p></div>
    <div className="connected-handwritten-note" aria-hidden="true">More channels.<br />Bigger impact.<span /></div>
  </header>
}

function MediaPlanEditBanner(props: PlanningContext & { mix: MediaMix; busy: boolean; act: ActionRunner; editable: boolean }) {
  const copy = props.editable
    ? 'Budgets, channel roles, flight dates and draft supply selections can be changed before reconfirmation.'
    : 'The approved strategy is immutable. Create a revision before changing budgets or flight dates.'
  return <section className={`connected-plan-edit-banner ${props.editable ? 'is-editing' : 'is-locked'}`}>
    <div><Icon name={props.editable ? 'plan' : 'shield'} /><span>
      <strong>{props.editable ? 'Media plan edit mode' : 'Approved media strategy'}</strong><small>{copy}</small></span></div>
    {!props.editable && <button className="secondary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))}>
      {props.busy ? 'Creating revision…' : 'Edit media plan'}</button>}
  </section>
}

function ChannelFilterbar({ mix, active, onChange }: {
  mix: MediaMix; active: string | null; onChange: (channel: string | null) => void
}) {
  return <div className="connected-channel-filterbar"><button type="button"
    className={active === null ? 'is-active' : ''} onClick={() => onChange(null)}>
    <Icon name="inventory" />All Channels</button>{mix.allocations.map(item => <button type="button" key={item.channel}
      className={active === item.channel ? 'is-active' : ''} onClick={() => onChange(item.channel)}>
      <MediaTypeIcon channel={item.channel} />{mediaVisual(item.channel).label}</button>)}</div>
}

function MediaPlanMain(props: MediaPlanningProps & {
  mix: MediaMix; selected: Shortlist['candidates']; editable: boolean; activeChannel: string | null
  setFlightChannel: (value: string | null) => void
  setReplaceCandidateId: (value: string | null) => void
}) {
  return <section className="connected-media-plan-main">
    <MediaPlanMainHeader {...props} />
    <MediaPlanRows {...props} />
    <ShortlistWorkbench {...props} />
    <MixWorkbench {...props} />
    <ConnectedPlanReview {...props} />
    <MediaPlanFooter {...props} />
  </section>
}

function MediaPlanMainHeader(props: MediaPlanningProps & { mix: MediaMix }) {
  return <header><div><h2>Selected media plan</h2>
    <p>Add, remove or adjust partners and placements across the approved channel mix.</p></div>
    <div>{props.shortlist
      ? <a className="text-action connected-add-placement" href="#shortlist-workbench">＋ Add Placement</a>
      : <button className="text-action connected-add-placement" type="button" disabled={props.busy}
        onClick={() => void props.act(() => planningApi.generateShortlist(props.tenantId, props.briefVersionId, props.token))}>
        {props.busy ? 'Finding supply…' : '＋ Add Placement'}</button>}
      <a className="secondary-button" href="#mix-editor">⌘ Bulk Edit</a></div>
  </header>
}

function MediaPlanRows(props: MediaPlanningProps & {
  mix: MediaMix; selected: Shortlist['candidates']; activeChannel: string | null
  setFlightChannel: (value: string | null) => void
  setReplaceCandidateId: (value: string | null) => void
}) {
  const allocations = props.activeChannel
    ? props.mix.allocations.filter(item => item.channel === props.activeChannel)
    : props.mix.allocations
  return <div className="connected-media-plan-table"><div className="connected-media-plan-head">
    <span /><span>Channel</span><span>Partner / Placement</span><span>Audience fit</span><span>Est. impressions</span>
    <span>Cost</span><span>Flight period</span><span>Market / Location</span><span>Actions</span></div>
    {allocations.map(allocation => {
      const candidate = candidateForChannel(props.selected, allocation.channel)
      return <MediaPlanRow key={allocation.channel} allocation={allocation} candidate={candidate}
        currency={props.mix.currency} onEditFlight={() => props.setFlightChannel(allocation.channel)}
        onReplace={candidate ? () => props.setReplaceCandidateId(candidate.id) : undefined} />
    })}
  </div>
}

function ShortlistWorkbench(props: MediaPlanningProps & { mix: MediaMix }) {
  if (!props.shortlist) return null
  const shortlist = props.shortlist
  return <details className="connected-plan-workbench" id="shortlist-workbench"><summary>Review shortlist and alternatives</summary>
    <ShortlistPanel tenantId={props.tenantId} token={props.token} shortlist={shortlist}
      requiredChannels={props.mix.allocations.filter(item => item.budgetMinor > 0).map(item => item.channel)}
      busy={props.busy} onConfirm={(candidateIds, reason) => props.act(() => planningApi.selectShortlist(
        props.tenantId, shortlist, candidateIds, props.token, reason))} />
  </details>
}

function MixWorkbench(props: MediaPlanningProps & { mix: MediaMix }) {
  return <details className="connected-plan-workbench" id="mix-editor"><summary>Edit budgets and flight dates</summary>
    <MediaMixEditor key={`${props.mix.id}-${props.mix.version}`} mix={props.mix}
      purchaseCandidates={props.shortlist?.candidates ?? []}
      allowedChannels={props.workspace.campaignMode?.allowedChannels ?? []} busy={props.busy}
      onSave={(allocations: MediaAllocation[]) => props.act(() => planningApi.updateMix(
        props.tenantId, props.mix, allocations, props.token))}
      onApprove={() => props.act(() => planningApi.approveMix(props.tenantId, props.mix, props.token))}
      onRevise={() => props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))} />
  </details>
}

function ConnectedPlanReview(props: MediaPlanningProps) {
  const plan = props.plan
  if (!plan) return null
  return <div id="media-plan-review"><MediaPlanPanel plan={plan} busy={props.busy}
    onResolve={(code, reason) => props.act(() => planningApi.resolveObjection(
      props.tenantId, plan, code, props.token, reason))}
    onApprove={() => props.act(async () => {
      await planningApi.approvePlan(props.tenantId, plan, props.token)
      notifications.success('Media plan approved for proposal preparation.')
    })} /></div>
}

function MediaPlanFooter(props: MediaPlanningProps & { mix: MediaMix }) {
  return <footer><Link className="secondary-button" to={`/planning/${props.workspace.briefVersionId}#strategy`}>
    ← Back: Strategy</Link><MediaPlanNextAction {...props} /></footer>
}

function MediaPlanNextAction(props: MediaPlanningProps) {
  if (props.plan && isApproved(props.plan.status)) return <Link className="primary-button"
    to={`/briefs/${props.workspace.briefId}/proposals/new`}>Next: Proposal →</Link>
  if (props.plan) return <a className="secondary-button" href="#media-plan-review">
    Review media plan before proposal</a>
  if (!props.shortlist || !isApproved(props.shortlist.status)) return null
  return <button className="primary-button" type="button" disabled={props.busy}
    onClick={() => void props.act(() => planningApi.generatePlan(props.tenantId, props.briefVersionId, props.token))}>
    {props.busy ? 'Building plan…' : 'Build client-ready media plan'}
  </button>
}

function MediaPlanSummary({ mix, shortlist, selected }: {
  mix: MediaMix; shortlist: Shortlist | null; selected: Shortlist['candidates']
}) {
  const metrics = planSummaryMetrics(mix, selected)
  return <aside className="connected-plan-summary"><article className="connected-plan-summary-card"><header><h2>Plan summary</h2>
    <a className="text-action" href="#media-plan-review">View Details →</a></header>
    <strong>{formatMoney(metrics.investmentMinor, mix.currency, 0)}</strong><small>Estimated supplier media cost</small>
    <dl><div><dt>Est. impressions</dt><dd>{compactPlanningNumber(metrics.impressions)}</dd></div>
      <div><dt>Avg. audience fit</dt><dd>{metrics.averageFit === null ? '—' : `${metrics.averageFit}%`}</dd></div>
      <div><dt>Channels</dt><dd>{mix.allocations.length}</dd></div><div><dt>Placements</dt><dd>{selected.length}</dd></div></dl>
    <h3>Selected channels</h3><div className="connected-selected-channels">{mix.allocations.map(item => <span key={item.channel}>
      <MediaTypeIcon channel={item.channel} />{mediaVisual(item.channel).label}</span>)}</div></article>
    <PlanningOpportunityMap shortlist={shortlist} />
    <PlanAssistant shortlist={shortlist} />
  </aside>
}

function PlanAssistant({ shortlist }: { shortlist: Shortlist | null }) {
  const copy = shortlist ? 'Compare eligible placements, scheduled supplier costs, audience evidence and geography before confirming the plan.'
    : 'Generate eligible supply so Advertified can compare placements against this strategy.'
  return <article className="connected-plan-ai"><span className="connected-ai-orb">✦</span><div>
    <h2>Need help optimising your plan?</h2><p>{copy}</p></div><span className="connected-plan-ai-arrow">→</span></article>
}

function planSummaryMetrics(mix: MediaMix, selected: Shortlist['candidates']) {
  const knownCosts = selected.map(item => item.suitability?.buyAssessment?.campaignSupplierCostMinor ?? null)
    .filter((value): value is number => value !== null)
  const investmentMinor = selected.length > 0 && knownCosts.length === selected.length
    ? knownCosts.reduce((sum, value) => sum + value, 0)
    : mix.totalBudgetMinor
  const impressions = completeMetric(selected.map(item => item.suitability?.buyAssessment?.impressions ?? null))
  const fits = selected.map(candidateFitPercent).filter((value): value is number => value !== null)
  const averageFit = fits.length === selected.length && fits.length > 0
    ? Math.round(fits.reduce((sum, value) => sum + value, 0) / fits.length)
    : null
  return { investmentMinor, impressions, averageFit }
}

function candidateFitPercent(candidate: Shortlist['candidates'][number]) {
  const value = candidate.suitability?.total ?? candidate.score
  if (value === null || value === undefined) return null
  return Math.round(value * (value <= 1 ? 100 : 1))
}

function completeMetric(values: Array<number | null>) {
  return values.length > 0 && values.every(value => value !== null)
    ? values.reduce((sum, value) => sum + (value ?? 0), 0)
    : null
}

function compactPlanningNumber(value: number | null) {
  if (value === null) return '—'
  if (value >= 1_000_000) return `${Math.round(value / 100_000) / 10}M`
  if (value >= 1_000) return `${Math.round(value / 100) / 10}K`
  return Math.round(value).toLocaleString('en-ZA')
}

function PlanningContextPanels(props: MediaPlanningProps) {
  return <>{props.workspace.decisionContext && <PlanningDecisionContext value={props.workspace.decisionContext} />}
    <PlanningMeasurementReadiness workspace={props.workspace} shortlist={props.shortlist} />
    <InventoryDecisionHistory key={`${props.tenantId}-${props.briefVersionId}`}
      tenantId={props.tenantId} briefVersionId={props.briefVersionId} /></>
}

function MediaPlanDrawers(props: MediaPlanningProps & {
  mix: MediaMix; selected: Shortlist['candidates']; editable: boolean
  flightChannel: string | null; replaceCandidateId: string | null
  setFlightChannel: (value: string | null) => void; setReplaceCandidateId: (value: string | null) => void
}) {
  const flight = props.mix.allocations.find(item => item.channel === props.flightChannel) ?? null
  const target = props.selected.find(item => item.id === props.replaceCandidateId) ?? null
  const flightCandidate = flight ? candidateForChannel(props.selected, flight.channel) : null
  return <>{flight && <FlightEditorDrawer allocation={flight} mix={props.mix} candidate={flightCandidate} editable={props.editable}
    busy={props.busy} onClose={() => props.setFlightChannel(null)}
    onCreateRevision={() => props.act(() => planningApi.generateMix(props.tenantId, props.briefVersionId, props.token))}
    onSave={allocations => props.act(() => planningApi.updateMix(props.tenantId, props.mix, allocations, props.token))} />}
    {target && props.shortlist && <ReplacementDrawer target={target} shortlist={props.shortlist}
      busy={props.busy} onClose={() => props.setReplaceCandidateId(null)}
      onStartRevision={() => props.act(() => planningApi.generateShortlist(props.tenantId, props.briefVersionId, props.token))}
      onReplace={(ids, reason) => props.act(() => planningApi.selectShortlist(
        props.tenantId, props.shortlist!, ids, props.token, reason))} />}</>
}

function selectedCandidates(shortlist: Shortlist | null) {
  return shortlist?.candidates.filter(item => item.isSelected) ?? []
}

function candidateForChannel(candidates: Shortlist['candidates'], channel: string) {
  return candidates.find(item => item.channel === channel) ?? null
}

function currentShortlist(workspace: PlanningWorkspace, mix: MediaMix | null): Shortlist | null {
  if (!mix || workspace.shortlist?.mixVersionId !== mix.id) return null
  return workspace.shortlist
}

function currentPlan(workspace: PlanningWorkspace, mix: MediaMix | null): MediaPlan | null {
  if (!mix || workspace.mediaPlan?.mixVersionId !== mix.id) return null
  return workspace.mediaPlan
}
