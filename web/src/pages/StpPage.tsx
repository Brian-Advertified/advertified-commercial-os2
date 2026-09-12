import { useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { AudienceStrategy, PlanningWorkspace } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { AudienceDecisionComparison } from '../brief-intake/AudienceDecisionComparison'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import '../audience-strategy.css'

type Context = {
  tenantId: string
  briefVersionId: string
  token: string
}
type AudienceRole = 'primary' | 'secondary' | 'excluded'
type AudienceSegment = AudienceStrategy['definitions'][number]
type AudienceAction = (action: () => Promise<unknown>) => Promise<boolean>

export function StpPage() {
  const briefVersionId = useParams().briefVersionId
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefVersionId || !session) return <Navigate to="/home" replace />
  return <StpWorkspace tenantId={selected.tenantId} briefVersionId={briefVersionId}
    token={session.antiforgeryToken} />
}

function StpWorkspace(context: Context) {
  const state = useAudienceWorkspace(context.tenantId, context.briefVersionId)
  if (state.error && !state.workspace)
    return <MessageState title="Audience planning could not be opened" message={state.error} />
  if (!state.workspace) return <LoadingState label="Loading audience strategy" />
  return <main className="page-shell audience-strategy-page">
    <CampaignModeBinding mode={state.workspace.campaignMode?.mode ?? null} />
    <header className="page-hero"><div><p className="eyebrow">Audience intelligence</p>
      <h1>Decide who the campaign should prioritise</h1>
      <p>Advertified separates client requirements, retained evidence and planning hypotheses so audience choices remain useful without manufacturing consumer facts.</p></div></header>
    {state.error && <p role="alert" className="form-error">{state.error}</p>}
    <AudienceWorkflow context={context} workspace={state.workspace} busy={state.busy} act={state.act} />
  </main>
}

function useAudienceWorkspace(tenantId: string, briefVersionId: string) {
  const [workspace, setWorkspace] = useState<PlanningWorkspace | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    let active = true
    void planningApi.getWorkspace(tenantId, briefVersionId)
      .then(value => { if (active) setWorkspace(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, briefVersionId])
  async function act(action: () => Promise<unknown>) {
    setBusy(true); setError(null)
    try {
      await action()
      setWorkspace(await planningApi.getWorkspace(tenantId, briefVersionId))
      return true
    } catch (failure) {
      setError(humanMessage(failure))
      return false
    } finally { setBusy(false) }
  }
  return { workspace, busy, error, act }
}

function AudienceWorkflow({ context, workspace, busy, act }: {
  context: Context
  workspace: PlanningWorkspace
  busy: boolean
  act: AudienceAction
}) {
  if (!workspace.campaignMode) return <CampaignModeChoice {...context} busy={busy} act={act} />
  if (!workspace.audience) return <StartAudienceDiscovery {...context} busy={busy} act={act} />
  if (workspace.audience.status === masterDataCodes.lifecycleStatuses.draft)
    return <AudienceStrategyReview {...context} audience={workspace.audience} busy={busy} act={act} />
  if (workspace.audience.status === masterDataCodes.lifecycleStatuses.approved)
    return <ApprovedAudienceStrategy audience={workspace.audience} />
  return null
}

function CampaignModeChoice(props: Context & {
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  return <section className="approved-stp-choice"><div><p className="eyebrow">Campaign scope</p>
    <h2>Confirm the media scope before audience discovery</h2>
    <p>The choice is locked for this Brief version. Changing from OOH-only to a full campaign requires a new campaign path.</p></div>
    <div className="approved-stp-actions"><button className="secondary-button" type="button" disabled={props.busy}
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
  audience: AudienceStrategy
  busy: boolean
  act: (action: () => Promise<unknown>) => Promise<boolean>
}) {
  const { audience } = props
  const navigate = useNavigate()
  const initialRoles = useMemo(() => rolesFromRecommendation(audience), [audience])
  const [roles, setRoles] = useState(initialRoles)
  const [rationale, setRationale] = useState(audience.targetingRationale ?? '')
  const [positioning, setPositioning] = useState(audience.positioningStatement ?? '')
  const primaryId = Object.entries(roles).find(([, role]) => role === 'primary')?.[0]
  const targetIds = selectedTargetIds(audience, roles, primaryId)
  const canApprove = Boolean(primaryId)
  function changeRole(id: string, role: AudienceRole) {
    setRoles(current => nextAudienceRoles(current, id, role))
  }
  async function submit() {
    if (!canApprove) return
    const approved = await props.act(() => planningApi.approveAudience(
      props.tenantId, audience, targetIds, rationale, positioning, props.token))
    if (approved) navigate(`/planning/${props.briefVersionId}`)
  }
  return <form className="audience-strategy-review" onSubmit={(event) => {
    event.preventDefault()
    void submit()
  }}>
    <AudienceStrategyIntro count={audience.definitions.length} />
    <AudienceDecisionComparison audience={audience} roles={roles} />
    <div className="audience-strategy-grid">
      {audience.definitions.map(item => <AudienceCard key={item.id} item={item}
        role={roles[item.id] ?? 'excluded'} onRole={role => changeRole(item.id, role)} />)}
    </div>
    <AudienceStrategyDirection rationale={rationale} positioning={positioning}
      setRationale={setRationale} setPositioning={setPositioning} />
    <AudienceStrategyApproval busy={props.busy} canApprove={canApprove} />
  </form>
}

function nextAudienceRoles(current: Record<string, AudienceRole>, id: string, role: AudienceRole) {
  const next = { ...current }
  if (role === 'primary') Object.keys(next).forEach(key => {
    if (next[key] === 'primary') next[key] = 'secondary'
  })
  next[id] = role
  return next
}

function AudienceStrategyIntro({ count }: { count: number }) {
  return <section className="audience-strategy-intro">
    <div><p className="eyebrow">Human review required</p>
      <h2>Choose the audiences the plan should prioritise</h2>
      <p>Recommendations are editable. The first selected audience is primary; others can support the campaign or be excluded.</p></div>
    <span>{count} candidates</span>
  </section>
}

function AudienceStrategyDirection({ rationale, positioning, setRationale, setPositioning }: {
  rationale: string
  positioning: string
  setRationale: (value: string) => void
  setPositioning: (value: string) => void
}) {
  return <section className="audience-strategy-direction">
    <label><span>Why these audiences</span>
      <textarea value={rationale} maxLength={4000} rows={4}
        onChange={event => setRationale(event.target.value)} /></label>
    <label><span>Positioning direction</span>
      <textarea value={positioning} maxLength={4000} rows={4}
        onChange={event => setPositioning(event.target.value)} /></label>
  </section>
}

function AudienceStrategyApproval({ busy, canApprove }: { busy: boolean; canApprove: boolean }) {
  return <footer className="audience-strategy-approval">
    <p>Approval records the signed-in reviewer and unlocks media planning. Self-approval is permitted for a single-owner agency.</p>
    <button className="primary-button" type="submit" disabled={busy || !canApprove}>
      {busy ? 'Approving audience strategy…' : 'Approve audience strategy & continue'}</button>
  </footer>
}

function AudienceCard({ item, role, onRole, readOnly = false }: {
  item: AudienceSegment
  role: AudienceRole
  onRole: (role: AudienceRole) => void
  readOnly?: boolean
}) {
  return <article className={`audience-strategy-card is-${role}`}>
    <AudienceCardHeader item={item} role={role} onRole={onRole} readOnly={readOnly} />
    <p>{item.description}</p>
    <AudienceFacts item={item} />
    <footer><span>{audienceEvidenceLabel(item)}</span><span>{audienceConfidenceLabel(item)}</span></footer>
    <AudienceWhy item={item} />
    <AudienceExclusions exclusions={item.exclusions} />
  </article>
}

function AudienceCardHeader({ item, role, onRole, readOnly }: {
  item: AudienceSegment
  role: AudienceRole
  onRole: (role: AudienceRole) => void
  readOnly: boolean
}) {
  return <header><div><small>{humanizeCode(item.classification, true)}</small><h3>{item.name}</h3></div>
    {readOnly ? <span className="audience-strategy-role">{humanizeCode(role, true)}</span>
      : <label><span>Planning role</span><select value={role}
        onChange={event => onRole(event.target.value as AudienceRole)}>
        <option value="primary">Primary</option><option value="secondary">Secondary</option>
        <option value="excluded">Exclude</option></select></label>}</header>
}

function AudienceFacts({ item }: { item: AudienceSegment }) {
  return <dl>
    <div><dt>Need</dt><dd>{item.needState ?? 'Not established / research required'}</dd></div>
    <div><dt>Buying context</dt><dd>{item.buyingContext ?? 'Not established / research required'}</dd></div>
    <div><dt>Where</dt><dd>{item.geographies.join(', ') || 'Not established'}</dd></div>
    <div><dt>Structured profile</dt><dd>{structuredProfile(item)}</dd></div>
  </dl>
}

function audienceEvidenceLabel(item: AudienceSegment) {
  if (item.evidenceItemIds.length > 0) return `${item.evidenceItemIds.length} approved evidence item${item.evidenceItemIds.length === 1 ? '' : 's'}`
  if (item.referenceObservationIds.length > 0) return `${item.referenceObservationIds.length} governed reference observation${item.referenceObservationIds.length === 1 ? '' : 's'}`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) return 'Client-required audience; research evidence not claimed'
  return 'No supporting evidence — validate as a hypothesis'
}

function audienceConfidenceLabel(item: AudienceSegment) {
  return item.confidence === null ? 'Confidence not established' : `${Math.round(item.confidence * 100)}% confidence`
}

function AudienceWhy({ item }: { item: AudienceSegment }) {
  return <details className="audience-strategy-why"><summary>Why this audience?</summary>
    <p>{audienceContextSentence(item)}</p><p>{audienceEvidenceSentence(item)}</p>
  </details>
}

function audienceContextSentence(item: AudienceSegment) {
  if (!item.needState && !item.buyingContext) return 'No verified need state or buying context is retained for this audience; those remain research gaps.'
  return `Retained audience context: need — ${item.needState ?? 'not established'}; buying context — ${item.buyingContext ?? 'not established'}.`
}

function audienceEvidenceSentence(item: AudienceSegment) {
  const geography = item.geographies.length > 0 ? `The retained geography is ${item.geographies.join(', ')}.` : 'No audience geography is established yet.'
  if (item.evidenceItemIds.length > 0) return `${geography} ${item.evidenceItemIds.length} approved evidence item${item.evidenceItemIds.length === 1 ? '' : 's'} support retained structured context.`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) return `${geography} The audience itself is a client requirement; no separate audience research is implied.`
  return `${geography} This remains a hypothesis until supporting evidence is retained.`
}

function AudienceExclusions({ exclusions }: { exclusions: readonly string[] }) {
  if (exclusions.length === 0) return null
  return <p className="audience-strategy-exclusions"><strong>Do not assume:</strong> {exclusions.join('; ')}</p>
}

function ApprovedAudienceStrategy({ audience }: { audience: AudienceStrategy }) {
  const roles = rolesFromRecommendation(audience)
  return <div className="audience-strategy-approved">
    <section className="audience-strategy-intro"><div><p className="eyebrow">Approved direction</p>
      <h2>Audiences guiding this campaign</h2>
      <p>The approved selection below is now the audience input for media allocation and inventory matching.</p></div>
      <span>{audience.targetAudienceIds.length} selected</span></section>
    <AudienceDecisionComparison audience={audience} roles={roles} />
    <div className="audience-strategy-grid">{audience.definitions.map(item =>
      <AudienceCard key={item.id} item={item} role={roles[item.id] ?? 'excluded'}
        onRole={() => undefined} readOnly />)}</div>
    <section className="audience-strategy-direction is-readonly">
      <article><h3>Why these audiences</h3><p>{audience.targetingRationale ?? 'Not established / research required.'}</p></article>
      <article><h3>Positioning direction</h3><p>{audience.positioningStatement ?? 'Not established / research required.'}</p></article>
    </section>
  </div>
}

function selectedTargetIds(audience: AudienceStrategy, roles: Record<string, AudienceRole>, primaryId?: string) {
  return [...(primaryId ? [primaryId] : []), ...audience.definitions
    .filter(item => roles[item.id] === 'secondary').map(item => item.id)]
}

function rolesFromRecommendation(audience: AudienceStrategy): Record<string, AudienceRole> {
  const targets = new Set(audience.targetAudienceIds)
  let primaryFound = false
  return Object.fromEntries(audience.definitions.map(item => {
    if (!targets.has(item.id)) return [item.id, 'excluded']
    if (!primaryFound) {
      primaryFound = true
      return [item.id, 'primary']
    }
    return [item.id, 'secondary']
  })) as Record<string, AudienceRole>
}

function structuredProfile(item: AudienceSegment) {
  const values = [item.language, item.lifeStage, item.lsmSem].filter(Boolean)
  return values.length > 0 ? values.join(' · ') : 'Not established / research required'
}
