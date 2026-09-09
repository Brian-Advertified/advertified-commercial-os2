import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import '../audience-strategy.css'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { AudienceSet, PlanningWorkspace } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { PlanningDecisionContext } from '../planning/PlanningDecisionContext'
import { humanizeCode } from '../presentation/format'

export function StpPage() {
  const briefVersionId = useParams().briefVersionId
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefVersionId || !session) return <Navigate to="/briefs" replace />
  return <AudienceStrategyRecord tenantId={selected.tenantId} briefVersionId={briefVersionId}
    token={session.antiforgeryToken} />
}

type Context = { tenantId: string; briefVersionId: string; token: string }
type AudienceRole = 'primary' | 'secondary' | 'excluded'
type AudienceDefinition = AudienceSet['definitions'][number]

function AudienceStrategyRecord(props: Context) {
  const state = useAudienceStrategy(props)
  if (state.error && !state.workspace) {
    return <MessageState title="Audience Strategy could not be opened" message={state.error} />
  }
  if (!state.workspace) return <LoadingState label="Loading Audience Strategy" />
  return <><CampaignModeBinding mode={state.workspace.campaignMode?.mode ?? null} />
    <AudienceStrategyContent {...props} workspace={state.workspace} busy={state.busy}
      error={state.error} act={state.act} /></>
}

function AudienceStrategyContent(props: Context & {
  workspace: PlanningWorkspace
  busy: boolean
  error: string | null
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  const { workspace } = props
  const approved = workspace.audience?.status === masterDataCodes.lifecycleStatuses.approved
  return <section className="approved-stp-page" aria-labelledby="stp-title">
    <Link className="text-action back-link" to={`/briefs/${workspace.briefId}`}>← Back to Brief</Link>
    <header className="approved-stp-header">
      <div><p className="eyebrow">Audience discovery and human validation</p>
        <h1 id="stp-title">Audience Strategy</h1>
        <p>Identify, compare and approve the audiences that should guide media planning.</p></div>
      {workspace.audience && <span className={`status-chip ${approved ? 'status-positive' : ''}`}>
        {humanizeCode(workspace.audience.status, true)}</span>}
    </header>
    {workspace.decisionContext && <PlanningDecisionContext value={workspace.decisionContext} />}
    {props.error && <p className="inline-alert" role="alert">{props.error}</p>}
    {!workspace.campaignMode
      ? <CampaignModeChoice {...props} />
      : !workspace.audience
        ? <StartAudienceDiscovery {...props} />
        : approved
          ? <ApprovedAudienceStrategy audience={workspace.audience} />
          : <AudienceStrategyReview {...props} audience={workspace.audience} />}
    {approved && <footer className="approved-stp-actions">
      <Link className="secondary-button" to={`/briefs/${workspace.briefId}`}>← Back</Link>
      <Link className="primary-button" to={`/planning/${workspace.briefVersionId}`}>
        Next: Media Planning →</Link>
    </footer>}
  </section>
}

function CampaignModeChoice(props: Context & {
  workspace: PlanningWorkspace
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  return <section className="approved-stp-choice"><div><p className="eyebrow">Media scope needs confirmation</p>
    <h2>Choose the campaign scope before audience discovery</h2>
    <p>The Brief did not establish whether this is Outdoor advertising or a full campaign. This decision is locked once planning starts.</p></div>
    <div><button className="secondary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.selectCampaignMode(
        props.tenantId, props.briefVersionId, masterDataCodes.campaignModes.oohOnly,
        props.token, { source: masterDataCodes.campaignModeDecisionSources.humanClarification,
          confidence: 1, reason: 'Human clarified Outdoor advertising scope before audience discovery.' }))}>Outdoor advertising and digital screens only</button>
      <button className="primary-button" type="button" disabled={props.busy}
        onClick={() => void props.act(() => planningApi.selectCampaignMode(
          props.tenantId, props.briefVersionId, masterDataCodes.campaignModes.fullCampaign,
          props.token, { source: masterDataCodes.campaignModeDecisionSources.humanClarification,
            confidence: 1, reason: 'Human clarified full campaign scope before audience discovery.' }))}>Full campaign</button></div>
  </section>
}

function StartAudienceDiscovery(props: Context & {
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  return <section className="approved-stp-choice"><div><p className="eyebrow">Audience discovery</p>
    <h2>Find the audiences most likely to move the campaign objective</h2>
    <p>Advertified will analyse the approved Brief and evidence, propose distinct candidate audiences, and label every unsupported conclusion as an inference or hypothesis.</p></div>
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.generateAudiences(
        props.tenantId, props.briefVersionId, props.token))}>
      {props.busy ? 'Discovering audiences…' : 'Discover candidate audiences'}</button>
  </section>
}

function AudienceStrategyReview(props: Context & {
  audience: AudienceSet
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  const { audience } = props
  const navigate = useNavigate()
  const initialRoles = useMemo(() => rolesFromRecommendation(audience), [audience])
  const [roles, setRoles] = useState(initialRoles)
  const [rationale, setRationale] = useState(audience.targetingRationale)
  const [positioning, setPositioning] = useState(audience.positioningStatement)
  const primaryId = Object.entries(roles).find(([, role]) => role === 'primary')?.[0]
  const targetIds = selectedTargetIds(audience, roles, primaryId)
  const canApprove = Boolean(primaryId && rationale.trim() && positioning.trim())
  function changeRole(id: string, role: AudienceRole) {
    setRoles(current => {
      const next = { ...current }
      if (role === 'primary') {
        for (const key of Object.keys(next)) {
          if (next[key] === 'primary') next[key] = 'secondary'
        }
      }
      next[id] = role
      return next
    })
  }
  return <form className="audience-strategy-review" onSubmit={(event) => {
    event.preventDefault()
    if (!canApprove) return
    void (async () => {
      const approved = await props.act(() => planningApi.approveAudience(
        props.tenantId, audience, targetIds, rationale, positioning, props.token))
      if (approved) navigate(`/planning/${props.briefVersionId}`)
    })()
  }}>
    <section className="audience-strategy-intro">
      <div><p className="eyebrow">Human review required</p>
        <h2>Choose the audiences the plan should prioritise</h2>
        <p>Recommendations are editable. The first selected audience is primary; others can support the campaign or be excluded.</p></div>
      <span>{audience.definitions.length} candidates</span>
    </section>
    <div className="audience-strategy-grid">
      {audience.definitions.map(item => <AudienceCard key={item.id} item={item}
        role={roles[item.id] ?? 'excluded'} onRole={role => changeRole(item.id, role)} />)}
    </div>
    <section className="audience-strategy-direction">
      <label><span>Why these audiences</span>
        <textarea value={rationale} maxLength={4000} rows={4}
          onChange={event => setRationale(event.target.value)} /></label>
      <label><span>Positioning direction</span>
        <textarea value={positioning} maxLength={4000} rows={4}
          onChange={event => setPositioning(event.target.value)} /></label>
    </section>
    <footer className="audience-strategy-approval">
      <p>Approval records the signed-in reviewer and unlocks media planning. Self-approval is permitted for a single-owner agency.</p>
      <button className="primary-button" type="submit" disabled={props.busy || !canApprove}>
        {props.busy ? 'Approving audience strategy…' : 'Approve audience strategy & continue'}</button>
    </footer>
  </form>
}

function AudienceCard({ item, role, onRole, readOnly = false }: {
  item: AudienceDefinition
  role: AudienceRole
  onRole: (role: AudienceRole) => void
  readOnly?: boolean
}) {
  const evidence = item.evidenceItemIds.length > 0
    ? `${item.evidenceItemIds.length} approved evidence item${item.evidenceItemIds.length === 1 ? '' : 's'}`
    : 'No supporting evidence — validate as a hypothesis'
  return <article className={`audience-strategy-card is-${role}`}>
    <header><div><small>{humanizeCode(item.classification, true)}</small>
      <h3>{item.name}</h3></div>
      {readOnly ? <span className="audience-strategy-role">{humanizeCode(role, true)}</span>
        : <label><span>Planning role</span><select value={role}
          onChange={event => onRole(event.target.value as AudienceRole)}>
          <option value="primary">Primary</option>
          <option value="secondary">Secondary</option>
          <option value="excluded">Exclude</option>
        </select></label>}</header>
    <p>{item.description}</p>
    <dl>
      <div><dt>Need</dt><dd>{item.needState}</dd></div>
      <div><dt>Buying context</dt><dd>{item.buyingContext}</dd></div>
      <div><dt>Where</dt><dd>{item.geographies.join(', ') || 'Not established'}</dd></div>
      <div><dt>Structured profile</dt><dd>{structuredProfile(item)}</dd></div>
    </dl>
    <footer><span>{evidence}</span><span>{Math.round(item.confidence * 100)}% confidence</span></footer>
    {item.exclusions.length > 0 && <p className="audience-strategy-exclusions">
      <strong>Do not assume:</strong> {item.exclusions.join('; ')}</p>}
  </article>
}

function ApprovedAudienceStrategy({ audience }: { audience: AudienceSet }) {
  const roles = rolesFromRecommendation(audience)
  return <div className="audience-strategy-approved">
    <section className="audience-strategy-intro"><div><p className="eyebrow">Approved direction</p>
      <h2>Audiences guiding this campaign</h2>
      <p>The approved selection below is now the audience input for media allocation and inventory matching.</p></div>
      <span>{audience.targetAudienceIds.length} selected</span></section>
    <div className="audience-strategy-grid">{audience.definitions.map(item =>
      <AudienceCard key={item.id} item={item} role={roles[item.id] ?? 'excluded'}
        onRole={() => undefined} readOnly />)}</div>
    <section className="audience-strategy-direction is-readonly">
      <article><h3>Why these audiences</h3><p>{audience.targetingRationale}</p></article>
      <article><h3>Positioning direction</h3><p>{audience.positioningStatement}</p></article>
    </section>
  </div>
}

function selectedTargetIds(audience: AudienceSet, roles: Record<string, AudienceRole>, primaryId?: string) {
  return [...(primaryId ? [primaryId] : []), ...audience.definitions
    .filter(item => roles[item.id] === 'secondary').map(item => item.id)]
}

function rolesFromRecommendation(audience: AudienceSet): Record<string, AudienceRole> {
  const targets = new Set(audience.targetAudienceIds)
  return Object.fromEntries(audience.definitions.map(item => [
    item.id,
    item.id === audience.targetAudienceIds[0]
      ? 'primary'
      : targets.has(item.id) ? 'secondary' : 'excluded',
  ]))
}

function structuredProfile(item: AudienceDefinition) {
  return [
    item.language && `Language: ${item.language}`,
    item.lifeStage && `Life stage: ${item.lifeStage}`,
    item.lsmSem && `LSM/SEM: ${item.lsmSem}`,
  ].filter(Boolean).join(' · ') || 'Not evidenced; no demographic assumption applied'
}

function useAudienceStrategy({ tenantId, briefVersionId }: Context) {
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
    try { await action(); await load(); return true }
    catch (failure) { setError(humanMessage(failure)); return false }
    finally { setBusy(false) }
  }
  return { workspace, busy, error, act }
}
