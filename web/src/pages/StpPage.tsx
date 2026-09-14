import { useEffect, useMemo, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { planningApi } from '../api/planning-client'
import type { AudienceResearchContext, AudienceStrategy, PlanningWorkspace } from '../api/planning-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { AudienceDecisionComparison } from '../brief-intake/AudienceDecisionComparison'
import { GeographicConcentration, AudienceEvidenceDashboard, AudienceInsights, AudienceRelevanceMatrix } from '../audience/AudienceInsightPanels'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import '../audience-strategy.css'
import '../audience-dashboard.css'

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
  return <main className="page-shell audience-strategy-page connected-audience-page">
    <CampaignModeBinding mode={state.workspace.campaignMode?.mode ?? null} />
    <header className="connected-stage-heading connected-audience-heading"><div><p className="eyebrow">New campaign</p>
      <h1>Audience &amp; STP</h1>
      <p>Define, explore and validate your target audience. Advertified combines approved Brief evidence with governed audience intelligence so you can reach the right people, in the right places, across the right channels.</p></div>
      <div className="connected-handwritten-note" aria-hidden="true">Turn ideas<br />into impact.<span /></div></header>
    <Link className="text-action connected-back-link" to={`/briefs/${state.workspace.briefId}#interpretation`}>
      ← Back to AI Interpretation
    </Link>
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
      const message = humanMessage(failure)
      try { setWorkspace(await planningApi.getWorkspace(tenantId, briefVersionId)) }
      catch { /* Keep the command failure visible if recovery loading also fails. */ }
      setError(message)
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
  const channels = workspace.decisionContext?.mediaJobs.map(item => item.channel) ?? []
  if (!workspace.campaignMode) return <CampaignModeChoice {...context} busy={busy} act={act} />
  if (!workspace.audience) return <StartAudienceDiscovery {...context} busy={busy} act={act} />
  if (workspace.audience.status === masterDataCodes.lifecycleStatuses.draft)
    return <AudienceStrategyReview {...context} audience={workspace.audience} research={workspace.audienceResearch}
      channels={channels} busy={busy} act={act} />
  if (workspace.audience.status === masterDataCodes.lifecycleStatuses.approved)
    return <ApprovedAudienceStrategy {...context} audience={workspace.audience} research={workspace.audienceResearch}
      channels={channels} busy={busy} act={act} />
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
    <h2>Research and build a usable audience strategy</h2>
    <p>Advertified will analyse the approved Brief, governed audience/reference evidence and campaign context. Where direct research is unavailable, it may propose clearly labelled working hypotheses for planner review instead of leaving the strategy empty.</p></div>
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.generateAudiences(
        props.tenantId, props.briefVersionId, props.token))}>
      {props.busy ? 'Researching audiences…' : 'Research & build audience strategy'}</button>
  </section>
}

function AudienceStrategyReview(props: Context & {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
  channels: readonly string[]
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
  const selectedTargets = audience.definitions.filter(item => targetIds.includes(item.id))
  const readinessIssues = audienceReadinessIssues(selectedTargets, rationale, positioning)
  const canApprove = Boolean(primaryId) && readinessIssues.length === 0
  function changeRole(id: string, role: AudienceRole) {
    setRoles(current => nextAudienceRoles(current, id, role))
  }
  async function submit() {
    if (!canApprove) return
    const approved = await props.act(() => planningApi.approveAudience(
      props.tenantId, audience, targetIds, rationale, positioning, props.token))
    if (approved) navigate(`/planning/${props.briefVersionId}#strategy`)
  }
  return <form className="audience-strategy-review connected-audience-workspace" onSubmit={(event) => {
    event.preventDefault()
    void submit()
  }}>
    <div className="connected-audience-top-grid">
      <div className="connected-audience-priority">
        {audience.definitions.slice(0, 2).map((item, index) => <AudienceSummaryCard key={item.id} item={item}
          role={roles[item.id] ?? (index === 0 ? 'primary' : 'secondary')}
          onRole={role => changeRole(item.id, role)} />)}
      </div>
      <GeographicConcentration audience={audience} research={props.research} />
    </div>
    <div className="connected-audience-content-grid">
      <section className="connected-audience-main-panel">
        <AudienceIntelligencePanel audience={audience} research={props.research} />
        <details className="connected-audience-comparison"><summary>Compare all retained audience evidence</summary>
          <AudienceDecisionComparison audience={audience} roles={roles} />
        </details>
      </section>
      <AudienceInsights audience={audience} research={props.research}
        rationale={rationale} positioning={positioning} />
    </div>
    <AudienceRelevanceMatrix audience={audience} research={props.research} channels={props.channels} />
    <AudienceStrategyDirection rationale={rationale} positioning={positioning}
      setRationale={setRationale} setPositioning={setPositioning} />
    <AdditionalAudienceCandidates audience={audience} roles={roles} onRole={changeRole} />
    <AudienceStrategyApproval busy={props.busy} canApprove={canApprove} issues={readinessIssues} />
  </form>
}

type AudienceIntelligenceTab = 'demographics' | 'psychographics' | 'channel' | 'income' | 'purchase'

function AudienceIntelligencePanel({ audience, research }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
}) {
  const [tab, setTab] = useState<AudienceIntelligenceTab>('demographics')
  const tabs: Array<[AudienceIntelligenceTab, string]> = [
    ['demographics', 'Demographics'], ['psychographics', 'Psychographics'], ['channel', 'Channel Affinity'],
    ['income', 'Household Income'], ['purchase', 'Purchase Intent'],
  ]
  return <>
    <div className="connected-audience-tabs" role="tablist" aria-label="Audience intelligence views">
      {tabs.map(([key, label]) => <button key={key} type="button" role="tab" aria-selected={tab === key}
        className={tab === key ? 'is-active' : ''} onClick={() => setTab(key)}>{label}</button>)}
    </div>
    {tab === 'demographics' ? <AudienceEvidenceDashboard audience={audience} research={research} />
      : <AudienceResearchGapPanel tab={tab} audience={audience} research={research} />}
  </>
}

function AudienceResearchGapPanel({ tab, audience, research }: {
  tab: AudienceIntelligenceTab
  audience: AudienceStrategy
  research: AudienceResearchContext | null
}) {
  if (tab === 'demographics') return null
  if (tab === 'channel') return <ChannelResearchContext audience={audience} research={research} />
  const copy: Record<Exclude<AudienceIntelligenceTab, 'demographics' | 'channel'>, [string, string]> = {
    psychographics: ['Psychographic profile', 'Values, attitudes and motivations need governed audience research before they can be presented as audience facts.'],
    income: ['Household income', 'Household-income distribution requires an approved source and cannot be inferred from audience labels or LSM / SEM alone.'],
    purchase: ['Purchase intent', 'Category intent, shopping behaviour and purchase timing require approved behavioural or research evidence.'],
  }
  const [title, message] = copy[tab]
  return <ResearchGapCard title={title} message={message} status={researchGapStatus(audience, research)} />
}

function ChannelResearchContext({ audience, research }: {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
}) {
  const rows = (research?.observations ?? []).filter(item =>
    item.dimensions.group === 'HOUSEHOLD_INTERNET_ACCESS' && item.metricCode === 'SHARE_PERCENT' &&
    item.geographyLevel !== 'COUNTRY' && ['Any kind of access', 'Mobile', 'Fixed Internet at home', 'Public Wi-Fi']
      .includes(item.dimensions.segment ?? ''))
  if (rows.length === 0) return <ResearchGapCard title="Channel affinity"
    message="Governed digital-access context is available only when a matching source exists; media preference, daypart and channel affinity still require audience-specific evidence."
    status={researchGapStatus(audience, research)} />
  return <section className="connected-audience-context-research">
    <header><div><span className="connected-ai-orb">✦</span><div><p className="eyebrow">Governed market context</p>
      <h3>Digital access by campaign market</h3></div></div><span>Not audience affinity</span></header>
    <p>These Stats SA household-access measures can inform channel planning context. They do not prove that a specific audience prefers a channel.</p>
    <div className="connected-context-research-grid">{rows.slice(0, 12).map(item => <article key={item.observationId}>
      <strong>{item.geographyName}</strong><span>{item.dimensions.segment}</span>
      <b>{formatResearchValue(item.metricValue, item.metricUnit)}</b>
      <small>{item.sourceTitle} · {item.measurementPeriod}</small>
    </article>)}</div>
  </section>
}

function ResearchGapCard({ title, message, status }: { title: string; message: string; status: string }) {
  return <section className="connected-audience-research-gap"><span className="connected-ai-orb">✦</span><div>
    <p className="eyebrow">Research layer</p><h3>{title}</h3><p>{message}</p><small>{status}</small>
  </div></section>
}

function researchGapStatus(audience: AudienceStrategy, research: AudienceResearchContext | null) {
  const supported = audience.definitions.filter(item => item.referenceObservationIds.length > 0 || item.evidenceItemIds.length > 0).length
  if (supported > 0) return `${supported} audience segment${supported === 1 ? '' : 's'} currently carry retained supporting evidence.`
  const contextCount = research?.observations.length ?? 0
  if (contextCount > 0) return `${contextCount} governed market-context observation${contextCount === 1 ? '' : 's'} are available, but none establishes this audience-specific factor.`
  return 'No separate supporting audience study is retained yet.'
}

function formatResearchValue(value: number, unit: string) {
  if (unit === 'PERCENT') return `${value.toFixed(value % 1 === 0 ? 0 : 1)}%`
  if (unit === 'PEOPLE') return new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 0 }).format(value)
  return `${value} ${unit}`
}

function AdditionalAudienceCandidates({ audience, roles, onRole }: {
  audience: AudienceStrategy
  roles: Record<string, AudienceRole>
  onRole: (id: string, role: AudienceRole) => void
}) {
  if (audience.definitions.length <= 2) return null
  return <details className="connected-additional-audiences"><summary>
    <span><strong>Additional audience candidates</strong><small>Review other segments without cluttering the main STP dashboard.</small></span>
    <b>{audience.definitions.length - 2}</b>
  </summary>
    <div>{audience.definitions.slice(2).map(item => <AudienceCard key={item.id} item={item}
      role={roles[item.id] ?? 'excluded'} onRole={role => onRole(item.id, role)} />)}</div>
  </details>
}

function nextAudienceRoles(current: Record<string, AudienceRole>, id: string, role: AudienceRole) {
  const next = { ...current }
  if (role === 'primary') Object.keys(next).forEach(key => {
    if (next[key] === 'primary') next[key] = 'secondary'
  })
  next[id] = role
  return next
}

function AudienceStrategyDirection({ rationale, positioning, setRationale, setPositioning }: {
  rationale: string
  positioning: string
  setRationale: (value: string) => void
  setPositioning: (value: string) => void
}) {
  return <details className="audience-strategy-direction connected-strategy-basis">
    <summary><span><strong>Strategy basis</strong><small>Review or edit the targeting rationale and positioning direction.</small></span>
      <b>Review</b></summary>
    <div className="connected-strategy-basis-grid">
      <label><span>Why these audiences</span>
        <textarea value={rationale} maxLength={4000} rows={4}
          onChange={event => setRationale(event.target.value)} /></label>
      <label><span>Positioning direction</span>
        <textarea value={positioning} maxLength={4000} rows={4}
          onChange={event => setPositioning(event.target.value)} /></label>
    </div>
  </details>
}

function AudienceStrategyApproval({ busy, canApprove, issues }: {
  busy: boolean
  canApprove: boolean
  issues: string[]
}) {
  return <footer className={`audience-strategy-approval${canApprove ? ' is-ready' : ' needs-enrichment'}`}>
    <div><strong>{canApprove ? 'Audience strategy ready for approval' : 'Audience intelligence needs enrichment'}</strong>
      {issues.length > 0
        ? <p>{issues.join(' ')}</p>
        : <p>Approval records the signed-in reviewer and unlocks Strategy.</p>}</div>
    <button className="primary-button" type="submit" disabled={busy || !canApprove}>
      {busy ? 'Approving audience strategy…' : 'Approve audience strategy & continue'}</button>
  </footer>
}

function AudienceSummaryCard({ item, role, onRole, readOnly = false }: {
  item: AudienceSegment
  role: AudienceRole
  onRole: (role: AudienceRole) => void
  readOnly?: boolean
}) {
  const profile = audienceSummaryProfile(item)
  return <article className={`connected-audience-summary-card is-${role}`}>
    <header>
      <div className="audience-card-identity">
        <span className="audience-card-icon"><Icon name="users" /></span>
        <div><small>{audienceRoleHeading(role)}</small><h3>{displayLabel(item.name)}</h3></div>
      </div>
      <details className="connected-audience-role-edit"><summary>{readOnly ? 'View' : 'Edit'}</summary>
        <div className="connected-audience-edit-panel">
          {!readOnly && <label><span>Audience role</span><select value={role}
            onChange={event => onRole(event.target.value as AudienceRole)}>
            <option value="primary">Primary</option><option value="secondary">Secondary</option>
            <option value="excluded">Exclude</option></select></label>}
          <div className="connected-audience-evidence-status"><span>{audienceEvidenceLabel(item)}</span>
            <span>{audienceConfidenceLabel(item)}</span></div>
          <details className="connected-audience-evidence-detail"><summary>Evidence &amp; research gaps</summary>
            <AudienceFacts item={item} /><AudienceWhy item={item} /><AudienceExclusions exclusions={item.exclusions} />
          </details>
        </div>
      </details>
    </header>
    {profile.length > 0 && <p className="connected-audience-profile-line">{profile.join('  |  ')}</p>}
    <p className="connected-audience-summary-copy">{item.description}</p>
  </article>
}

function audienceSummaryProfile(item: AudienceSegment) {
  return [item.lifeStage, item.lsmSem, compactGeographyLabel(item.geographies)].filter((value): value is string => Boolean(value))
}

function compactGeographyLabel(geographies: readonly string[]) {
  if (geographies.length === 0) return null
  if (geographies.length <= 2) return geographies.map(displayLabel).join(' · ')
  return `${displayLabel(geographies[0])} +${geographies.length - 1} areas`
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
  return <header className="audience-card-header"><div className="audience-card-identity">
    <span className="audience-card-icon"><Icon name="users" /></span><div>
      <small>{audienceRoleHeading(role)}</small><h3>{displayLabel(item.name)}</h3>
      <em>{humanizeCode(item.classification, true)}</em></div></div>
    {readOnly ? <span className="audience-strategy-role">{humanizeCode(role, true)}</span>
      : <label><span>Edit role</span><select value={role}
        onChange={event => onRole(event.target.value as AudienceRole)}>
        <option value="primary">Primary</option><option value="secondary">Secondary</option>
        <option value="excluded">Exclude</option></select></label>}</header>
}

function audienceRoleHeading(role: AudienceRole) {
  if (role === 'primary') return 'Primary Audience'
  if (role === 'secondary') return 'Secondary Audience'
  return 'Excluded Audience'
}

function AudienceFacts({ item }: { item: AudienceSegment }) {
  return <dl>
    <AudienceFact label="Need" value={item.needState} />
    <AudienceFact label="Buying context" value={item.buyingContext} />
    <div><dt>Where</dt><dd>{item.geographies.map(displayLabel).join(', ') || 'Not established'}</dd></div>
    <div><dt>Structured profile</dt><dd>{structuredProfile(item)}</dd></div>
  </dl>
}

function AudienceFact({ label, value }: { label: string; value: string | null }) {
  const hypothesis = isHypothesis(value)
  return <div><dt>{label}</dt><dd>{value ?? 'Research required'}
    {hypothesis && <em className="audience-field-status is-hypothesis">AI hypothesis</em>}</dd></div>
}

function audienceEvidenceLabel(item: AudienceSegment) {
  if (item.evidenceItemIds.length > 0) return `${item.evidenceItemIds.length} approved evidence item${item.evidenceItemIds.length === 1 ? '' : 's'}`
  if (item.referenceObservationIds.length > 0) return `${item.referenceObservationIds.length} governed reference observation${item.referenceObservationIds.length === 1 ? '' : 's'}`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) {
    return isHypothesis(item.needState) || isHypothesis(item.buyingContext)
      ? 'Client requirement · AI context hypotheses need validation'
      : 'Client requirement'
  }
  return 'No supporting evidence — validate as a hypothesis'
}

function audienceConfidenceLabel(item: AudienceSegment) {
  if (item.confidence !== null) return `${Math.round(item.confidence * 100)}% confidence`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) return 'Client requirement'
  if (item.classification === masterDataCodes.evidenceClassifications.hypothesis) return 'Working hypothesis — validate'
  return 'Confidence not established'
}

function AudienceWhy({ item }: { item: AudienceSegment }) {
  return <details className="audience-strategy-why"><summary>Why this audience?</summary>
    <p>{audienceContextSentence(item)}</p><p>{audienceEvidenceSentence(item)}</p>
  </details>
}

function audienceContextSentence(item: AudienceSegment) {
  if (!item.needState && !item.buyingContext) return 'No audience need or buying-context hypothesis has been retained yet; research is still required.'
  const needLabel = isHypothesis(item.needState) ? 'working need hypothesis' : 'retained need'
  const buyingLabel = isHypothesis(item.buyingContext) ? 'working buying-context hypothesis' : 'retained buying context'
  return `${needLabel}: ${item.needState ?? 'not established'}; ${buyingLabel}: ${item.buyingContext ?? 'not established'}.`
}

function audienceEvidenceSentence(item: AudienceSegment) {
  const geography = item.geographies.length > 0 ? `The retained geography is ${item.geographies.map(displayLabel).join(', ')}.` : 'No audience geography is established yet.'
  if (item.evidenceItemIds.length > 0) return `${geography} ${item.evidenceItemIds.length} approved evidence item${item.evidenceItemIds.length === 1 ? '' : 's'} support retained structured context.`
  if (item.classification === masterDataCodes.evidenceClassifications.clientRequirement) return `${geography} The audience itself is a client requirement; no separate audience research is implied.`
  return `${geography} This remains a hypothesis until supporting evidence is retained.`
}

function AudienceExclusions({ exclusions }: { exclusions: readonly string[] }) {
  if (exclusions.length === 0) return null
  return <p className="audience-strategy-exclusions"><strong>Do not assume:</strong> {exclusions.join('; ')}</p>
}

function ApprovedAudienceStrategy(props: Context & {
  audience: AudienceStrategy
  research: AudienceResearchContext | null
  channels: readonly string[]
  busy: boolean
  act: AudienceAction
}) {
  const { audience } = props
  const roles = rolesFromRecommendation(audience)
  const targets = audience.definitions.filter(item => audience.targetAudienceIds.includes(item.id))
  const issues = audienceReadinessIssues(targets, audience.targetingRationale ?? '', audience.positioningStatement ?? '')
  return <div className="audience-strategy-approved connected-audience-workspace">
    <AudienceEnrichmentNotice {...props} issues={issues} />
    <ApprovedAudienceTop audience={audience} roles={roles} research={props.research} />
    <ApprovedAudienceEvidence audience={audience} roles={roles} research={props.research} channels={props.channels} />
    <AudienceDirectionReadOnly audience={audience} />
    <ApprovedAudienceFooter audience={audience} issues={issues} />
  </div>
}

function AudienceEnrichmentNotice(props: Context & {
  busy: boolean
  act: AudienceAction
  issues: string[]
}) {
  if (props.issues.length === 0) return null
  return <section className="audience-enrichment-banner" role="status"><div>
    <strong>This saved audience strategy needs enrichment</strong>
    <p>{props.issues.join(' ')}</p></div>
    <button className="primary-button" type="button" disabled={props.busy}
      onClick={() => void props.act(() => planningApi.generateAudiences(
        props.tenantId, props.briefVersionId, props.token))}>
      {props.busy ? 'Researching audiences…' : 'Research & rebuild strategy'}</button></section>
}

function ApprovedAudienceTop({ audience, roles, research }: {
  audience: AudienceStrategy
  roles: Record<string, AudienceRole>
  research: AudienceResearchContext | null
}) {
  const visible = audience.definitions.filter(item => roles[item.id] !== 'excluded').slice(0, 2)
  return <div className="connected-audience-top-grid">
    <div className="connected-audience-priority">{visible.map(item =>
      <AudienceSummaryCard key={item.id} item={item} role={roles[item.id] ?? 'secondary'}
        onRole={() => undefined} readOnly />)}</div>
    <GeographicConcentration audience={audience} research={research} />
  </div>
}

function ApprovedAudienceEvidence({ audience, roles, research, channels }: {
  audience: AudienceStrategy
  roles: Record<string, AudienceRole>
  research: AudienceResearchContext | null
  channels: readonly string[]
}) {
  return <>
    <div className="connected-audience-content-grid">
      <section className="connected-audience-main-panel">
        <AudienceIntelligencePanel audience={audience} research={research} />
        <details className="connected-audience-comparison"><summary>Compare retained audience evidence</summary>
          <AudienceDecisionComparison audience={audience} roles={roles} /></details>
      </section>
      <AudienceInsights audience={audience} research={research} rationale={audience.targetingRationale ?? ''}
        positioning={audience.positioningStatement ?? ''} />
    </div>
    <AudienceRelevanceMatrix audience={audience} research={research} channels={channels} />
  </>
}

function AudienceDirectionReadOnly({ audience }: { audience: AudienceStrategy }) {
  return <section className="audience-strategy-direction is-readonly">
    <article><h3>Why these audiences</h3><p>{audience.targetingRationale ?? 'Research required.'}</p></article>
    <article><h3>Positioning direction</h3><p>{audience.positioningStatement ?? 'Research required.'}</p></article>
  </section>
}

function ApprovedAudienceFooter({ audience, issues }: { audience: AudienceStrategy; issues: string[] }) {
  if (issues.length > 0) return <div className="connected-audience-approved-footer needs-enrichment">
    <span>Audience strategy needs enrichment before Strategy</span></div>
  return <div className="connected-audience-approved-footer"><span>✓ Audience strategy approved</span>
    <Link className="primary-button" to={`/planning/${audience.briefVersionId}#strategy`}>Next: Strategy →</Link></div>
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

function audienceReadinessIssues(targets: AudienceSegment[], rationale: string, positioning: string) {
  const issues: string[] = []
  if (targets.length === 0) issues.push('Choose at least one target audience.')
  const withoutContext = targets.filter(item => !hasUsableAudienceContext(item))
  if (withoutContext.length > 0) issues.push(`Add need, buying context or retained evidence for ${withoutContext.map(item => item.name).join(', ')}.`)
  if (!rationale.trim()) issues.push('Add a targeting rationale.')
  if (!positioning.trim()) issues.push('Add a positioning direction or clearly labelled working hypothesis.')
  return issues
}

function hasUsableAudienceContext(item: AudienceSegment) {
  return Boolean(item.needState || item.buyingContext || item.language || item.lifeStage || item.lsmSem ||
    item.evidenceItemIds.length > 0 || item.referenceObservationIds.length > 0)
}

function isHypothesis(value: string | null) {
  return value?.trim().toLowerCase().startsWith('hypothesis:') ?? false
}

function displayLabel(value: string) {
  return value.trim().replace(/[ .,:;!?]+$/, '')
}

function structuredProfile(item: AudienceSegment) {
  const values = [item.language, item.lifeStage, item.lsmSem].filter(Boolean)
  return values.length > 0 ? values.join(' · ') : 'Not established / research required'
}
