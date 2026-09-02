import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { AudienceSet, PlanningWorkspace } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'

export function StpPage() {
  const briefVersionId = useParams().briefVersionId
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefVersionId || !session) return <Navigate to="/briefs" replace />
  return <StpRecord tenantId={selected.tenantId} briefVersionId={briefVersionId}
    token={session.antiforgeryToken} />
}

type Context = { tenantId: string; briefVersionId: string; token: string }

function StpRecord(props: Context) {
  const state = useStp(props)
  if (state.error && !state.workspace) {
    return <MessageState title="Strategy & STP could not be opened" message={state.error} />
  }
  if (!state.workspace) return <LoadingState label="Loading Strategy & STP" />
  return <><CampaignModeBinding mode={state.workspace.campaignMode?.mode ?? null} />
    <StpContent {...props} workspace={state.workspace} busy={state.busy}
      error={state.error} act={state.act} /></>
}

function StpContent(props: Context & {
  workspace: PlanningWorkspace
  busy: boolean
  error: string | null
  act: (action: () => Promise<unknown>) => Promise<void>
}) {
  const { workspace } = props
  return <section className="approved-stp-page" aria-labelledby="stp-title">
    <Link className="text-action back-link" to={`/briefs/${workspace.briefId}`}>← Back to Brief</Link>
    <header className="approved-stp-header">
      <div><p className="eyebrow">AI-powered research and human validation</p>
        <h1 id="stp-title">Strategy & STP</h1>
        <p>Define who the campaign should reach, why they matter and what the campaign should establish before media allocation begins.</p></div>
      {workspace.audience && <span className="status-chip status-positive">
        {humanizeCode(workspace.audience.status, true)}</span>}
    </header>
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {!workspace.campaignMode
      ? <CampaignModeChoice {...props} />
      : workspace.audience
        ? <ApprovedStp audience={workspace.audience} />
        : <StartStp {...props} />}
    {workspace.audience && <footer className="approved-stp-actions">
      <Link className="secondary-button" to={`/briefs/${workspace.briefId}`}>← Back</Link>
      <Link className="primary-button" to={`/planning/${workspace.briefVersionId}`}>Next: Media Planning →</Link>
    </footer>}
  </section>
}

function CampaignModeChoice(props: Context & {
  workspace: PlanningWorkspace
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<void>
}) {
  return <section className="approved-stp-choice"><div><p className="eyebrow">Media scope needs confirmation</p>
    <h2>Choose the campaign scope before Strategy & STP</h2>
    <p>The Brief did not establish whether this is OOH-only or a full campaign. This decision is locked once planning starts.</p></div>
    <div><button className="secondary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.selectCampaignMode(
        props.tenantId, props.briefVersionId, masterDataCodes.campaignModes.oohOnly,
        props.token, { source: masterDataCodes.campaignModeDecisionSources.humanClarification,
          confidence: 1, reason: 'Human clarified OOH-only scope before Strategy & STP.' }))}>OOH / DOOH only</button>
      <button className="primary-button" type="button" disabled={props.busy}
        onClick={() => void props.act(() => planningApi.selectCampaignMode(
          props.tenantId, props.briefVersionId, masterDataCodes.campaignModes.fullCampaign,
          props.token, { source: masterDataCodes.campaignModeDecisionSources.humanClarification,
            confidence: 1, reason: 'Human clarified full campaign scope before Strategy & STP.' }))}>Full campaign</button></div>
  </section>
}

function StartStp(props: Context & {
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<void>
}) {
  return <section className="approved-stp-choice"><div><p className="eyebrow">Strategy & STP</p>
    <h2>Build the audience and positioning direction</h2>
    <p>Advertified will create evidence-labelled segmentation, targeting and positioning from the approved Brief.</p></div>
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.generateAudiences(
        props.tenantId, props.briefVersionId, props.token))}>
      {props.busy ? 'Building Strategy & STP…' : 'Generate Strategy & STP'}</button>
  </section>
}

function ApprovedStp({ audience }: { audience: AudienceSet }) {
  const targeted = new Set(audience.targetAudienceIds)
  return <div className="approved-stp-workspace">
    <aside className="approved-stp-localnav" aria-label="Strategy and STP sections">
      {['Summary', 'Segmentation', 'Targeting', 'Positioning', 'Insights', 'Review'].map((item, index) =>
        <a className={index === 0 ? 'is-active' : ''} href={`#stp-${item.toLowerCase()}`} key={item}>{item}</a>)}
    </aside>
    <main className="approved-stp-main" id="stp-summary">
      <section className="approved-stp-segments" id="stp-segmentation"><header><h2>Who we will reach</h2></header>
        <div className="approved-stp-tabs"><span className="is-active">Demographics</span><span>Psychographics</span><span>Behaviours</span></div>
        {audience.definitions.map(item => <article key={item.id} className={targeted.has(item.id) ? 'is-targeted' : ''}>
          <span>{targeted.has(item.id) ? '●' : '○'}</span><div><small>{targeted.has(item.id) ? 'Primary / Secondary Audience' : 'Additional Audience'}</small>
            <strong>{item.name}</strong><p>{item.description}</p>
            <em>{item.geographies.join(' · ') || 'Geography not supplied'} · {humanizeCode(item.classification, true)}</em></div></article>)}
      </section>
      <section className="approved-stp-insights" id="stp-insights"><header><h2>Key Insights</h2></header>
        <ul>{audience.definitions.slice(0, 4).map(item => <li key={item.id}>✓ {item.needState || item.buyingContext || item.description}</li>)}</ul></section>
      <section className="approved-stp-positioning" id="stp-positioning"><header><h2>Positioning Statement</h2></header><p>{audience.positioningStatement}</p></section>
      <section className="approved-stp-targeting" id="stp-targeting"><header><h2>Targeting rationale</h2></header><p>{audience.targetingRationale}</p></section>
      <section className="approved-stp-review" id="stp-review"><header><h2>Review</h2></header>
        <p>Strategy & STP is {humanizeCode(audience.status, true)} and ready to feed the media-planning stage.</p></section>
    </main>
  </div>
}

function useStp({ tenantId, briefVersionId }: Context) {
  const [workspace, setWorkspace] = useState<PlanningWorkspace | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const load = useCallback(async () => {
    setWorkspace(await planningApi.getWorkspace(tenantId, briefVersionId)); setError(null)
  }, [tenantId, briefVersionId])
  useEffect(() => { let active = true; void planningApi.getWorkspace(tenantId, briefVersionId)
    .then(value => { if (active) setWorkspace(value) })
    .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false } }, [tenantId, briefVersionId])
  async function act(action: () => Promise<unknown>) {
    setBusy(true); setError(null)
    try { await action(); await load() } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { workspace, busy, error, act }
}
