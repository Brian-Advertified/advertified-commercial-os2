import { useCallback, useEffect, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { briefApi } from '../api/brief-client'
import { humanMessage } from '../api/client'
import { opportunityCodes } from '../api/opportunity-constants'
import { planningApi } from '../api/planning-client'
import type { CampaignMode } from '../api/planning-schemas'
import type { CampaignBrief, BriefVersion } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignModeBinding } from '../campaign-flow/CampaignFlowBindings'
import {
  BriefLocalNavigation,
  BriefStep,
} from '../brief/BriefSectionFlow'
import {
  type BriefSectionId,
  type BriefSectionState,
  useBriefSectionFlow,
} from '../brief/brief-section-flow-state'
import { BriefSummary, Field, ListFields } from '../brief/BriefPageParts'
import { buildSectionStates } from '../brief/brief-section-status'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDateTime, formatMoney, humanizeCode } from '../presentation/format'

const briefConfirmerRoles: readonly string[] = Object.values(opportunityCodes.briefConfirmerRole)

export function BriefPage() {
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  const { briefId } = useParams()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!briefId || !session) return <Navigate to="/briefs" replace />
  return <BriefRecord tenantId={selected.tenantId} briefId={briefId}
    token={session.antiforgeryToken}
    canConfirm={briefConfirmerRoles.includes(selected.roleCode)} />
}

function BriefRecord({ tenantId, briefId, token, canConfirm }: {
  tenantId: string
  briefId: string
  token: string
  canConfirm: boolean
}) {
  const model = useBriefRecord(tenantId, briefId)
  if (model.error && !model.record) {
    return <MessageState title="Brief could not be opened" message={model.error} />
  }
  if (!model.record) return <LoadingState label="Loading the campaign Brief" />
  const current = model.record.versions.at(-1)
  if (!current) return <MessageState title="Brief is incomplete"
    message="No retained Brief version is available for review." />
  return <><CampaignModeBinding mode={model.campaignMode?.mode ?? null} />
  <section className="brief-record-page approved-brief-page" aria-labelledby="brief-title">
    {model.error && <p className="inline-alert" role="alert">{model.error}</p>}
    <BriefScreen record={model.record} version={current} campaignMode={model.campaignMode}
      approved={current.status === opportunityCodes.status.approved}
      allowed={canConfirm && isConfirmable(current.status)} busy={model.busy}
      onConfirm={() => model.confirm(current, token)} />
  </section></>
}

type BriefScreenProps = {
  record: CampaignBrief
  version: BriefVersion
  campaignMode: CampaignMode | null
  approved: boolean
  allowed: boolean
  busy: boolean
  onConfirm: () => Promise<void>
}

type BriefStepResolver = (id: BriefSectionId) => {
  section: BriefSectionState
  active: boolean
  previous: BriefSectionState | null
  next: BriefSectionState | null
  onSelect: (id: BriefSectionId) => void
}

function BriefScreen(props: BriefScreenProps) {
  const flow = useBriefSectionFlow()
  const view = briefPresentation(props)
  const step = createStepResolver(view.sections, flow.activeId, flow.goTo)
  return <>
    <header className="approved-brief-page-title">
      <div><Link className="text-action" to="/briefs">← Briefs</Link>
        <h1 id="brief-title">Review Campaign Brief</h1>
        <p>{props.record.brief.clientName} · {props.record.brief.title} ·
          Version {props.version.versionNumber}</p></div>
      <div><span className="status-chip">
        {humanizeCode(props.version.status, true)}</span></div>
    </header>
    <div className="approved-brief-layout">
      <BriefLocalNavigation sections={view.sections} activeId={flow.activeId}
        onSelect={flow.goTo} />
      <main className="approved-brief-main">
        <BriefCoreSteps record={props.record} version={props.version}
          budget={view.budget} step={step} />
        <BriefGovernanceSteps {...props} {...view} step={step} />
      </main>
      <BriefSummary version={props.version} budget={view.budget}
        completeness={view.completeness}
        attention={view.sections.length - view.completeCount} />
    </div>
  </>
}

function BriefCoreSteps({ record, version, budget, step }: {
  record: CampaignBrief
  version: BriefVersion
  budget: string
  step: BriefStepResolver
}) {
  return <>
    <BriefStep {...step('overview')}
      copy="Describe the business challenge and what success looks like.">
      <article className="approved-brief-overview-copy">
        <p>{version.businessProblem || 'Not supplied'}</p>
        <span>{version.businessProblem ? '✓ Extracted from Brief' : '! Needs attention'}</span>
      </article>
      <Field label="Campaign name" value={record.brief.title} />
      <Field label="Business problem" value={version.businessProblem || 'Not supplied'} wide />
    </BriefStep>
    <BriefStep {...step('objectives')} copy="What must this campaign achieve?">
      <Field label="Campaign objective" value={version.objective || 'Not supplied'} wide />
      <Field label="Primary KPI" value={version.measurement[0] ?? 'Not supplied'} />
      <Field label="Success target" value={version.measurement[1] ?? 'Not supplied'} />
    </BriefStep>
    <BriefStep {...step(masterDataCodes.agentTypes.audience)} copy="Who should the campaign influence?">
      <ListFields label="Audience" values={version.audiences} />
    </BriefStep>
    <BriefStep {...step('geography')} copy="Where must the campaign work?">
      <ListFields label="Geography" values={version.geographies} />
    </BriefStep>
    <BriefStep {...step('timing')} copy="When must the campaign run?">
      <Field label="Campaign timing" value={version.timing || 'Not supplied'} wide />
    </BriefStep>
    <BriefStep {...step('budget')} copy="The commercial boundary for planning.">
      <Field label="Budget" value={budget} />
      <Field label="VAT status" value={version.vatStatus
        ? humanizeCode(version.vatStatus, true) : 'Not supplied'} />
    </BriefStep>
  </>
}

function BriefGovernanceSteps(props: BriefScreenProps & {
  step: BriefStepResolver
  unresolved: BriefVersion['conflicts']
  sections: BriefSectionState[]
  completeCount: number
  completeness: number
  budget: string
  campaignType: string
  modeSource: string
  readyForApproval: boolean
}) {
  return <>
    <BriefStep {...props.step('media')}
      copy="Confirm the immutable media scope before Strategy & STP.">
      <Field label="Campaign type" value={props.campaignType} />
      <Field label="Decision source" value={props.modeSource} />
      <Field label="Decision rationale"
        value={props.campaignMode?.reason ?? 'The media scope is still materially unclear.'} wide />
    </BriefStep>
    <BriefStep {...props.step('constraints')} copy="Rules the plan may not violate.">
      <ListFields label="Constraint" values={props.version.constraints} />
    </BriefStep>
    <BriefStep {...props.step(masterDataCodes.agentTypes.measurement)} copy="How success will be assessed.">
      <ListFields label="Measure" values={props.version.measurement} />
    </BriefStep>
    <BriefStep {...props.step('attachments')}
      copy="Original sources retained with this Brief.">
      <BriefSources record={props.record} />
    </BriefStep>
    <BriefReviewStep {...props} />
  </>
}

function BriefSources({ record }: { record: CampaignBrief }) {
  return <div className="approved-brief-source-list">{record.sources.map(source =>
    <article key={source.id}><strong>{source.title}</strong>
      <small>{humanizeCode(source.sourceType, true)} ·
        {formatDateTime(source.createdAtUtc)}</small>
      <details><summary>View original source</summary>
        <pre>{source.content}</pre></details></article>)}</div>
}

function BriefReviewStep(props: BriefScreenProps & {
  step: BriefStepResolver
  unresolved: BriefVersion['conflicts']
  campaignType: string
  modeSource: string
  readyForApproval: boolean
}) {
  const finalAction = <BriefFinalAction {...props} />
  return <BriefStep {...props.step('review')}
    copy="Only unresolved material items block the next step."
    finalAction={finalAction}>
    <div className="approved-brief-review-grid">
      <article className="approved-brief-mode-review"><span>Campaign type</span>
        <strong>{props.campaignType}</strong>
        <small>{props.modeSource} ·
          {props.campaignMode?.reason ?? 'Mode decision still required.'}</small></article>
      <article><strong>{props.version.facts.length}</strong><span>recorded facts</span></article>
      <article><strong>{props.version.unknowns.length}</strong><span>open questions</span></article>
      <article><strong>{props.unresolved.length}</strong><span>unresolved conflicts</span></article>
    </div>
    {props.version.unknowns.map((item, index) =>
      <p className="approved-brief-review-item" key={`unknown-${index}`}>
        <strong>{humanizeCode(item.fieldPath, true)}</strong> · {item.question}</p>)}
    {props.unresolved.map((item, index) =>
      <p className="approved-brief-review-item" key={`conflict-${index}`}>
        <strong>{humanizeCode(item.fieldPath, true)}</strong> · {item.description}</p>)}
  </BriefStep>
}

function BriefFinalAction(props: BriefScreenProps & { readyForApproval: boolean }) {
  return <div className="approved-brief-final-actions">
    {!props.readyForApproval && <span className="approved-brief-blocker">
      Resolve the campaign type and material review items before approval.</span>}
    {props.allowed && <button className="primary-button" type="button"
      disabled={props.busy || !props.readyForApproval}
      onClick={() => void props.onConfirm()}>
      {briefDecisionLabel(props.version.status, props.busy)}</button>}
    {props.approved && <Link className="primary-button" to={`/stp/${props.version.id}`}>
      Next: Strategy & STP →</Link>}
    {!props.allowed && !props.approved && <span className="approved-brief-blocker">
      Awaiting an authorised Brief approver.</span>}
  </div>
}

function briefDecisionLabel(status: string, busy: boolean) {
  if (busy) return 'Saving the decision…'
  return status === opportunityCodes.status.draft
    ? 'Submit Brief for approval'
    : 'Approve Brief and continue'
}

function briefPresentation(props: BriefScreenProps) {
  const unresolved = props.version.conflicts.filter(item => !item.resolved)
  const sections = buildSectionStates(
    props.record, props.version, props.campaignMode, props.approved)
  const completeCount = sections.filter(item => item.status === 'complete').length
  const modeSource = props.campaignMode
    ? humanizeCode(props.campaignMode.decisionSource, true)
    : 'Awaiting mode decision'
  return {
    unresolved,
    sections,
    completeCount,
    completeness: Math.round(completeCount / sections.length * 100),
    budget: briefBudget(props.version),
    campaignType: campaignTypeLabel(props.campaignMode),
    modeSource,
    readyForApproval: props.campaignMode !== null &&
      !props.version.unknowns.some(item => item.isBlocking) &&
      unresolved.length === 0,
  }
}

function createStepResolver(
  sections: BriefSectionState[],
  activeId: BriefSectionId,
  onSelect: (id: BriefSectionId) => void,
): BriefStepResolver {
  const activeIndex = Math.max(0, sections.findIndex(item => item.id === activeId))
  return (id) => {
    const index = sections.findIndex(item => item.id === id)
    return {
      section: sections[index],
      active: activeIndex === index,
      previous: sections[index - 1] ?? null,
      next: sections[index + 1] ?? null,
      onSelect,
    }
  }
}

function briefBudget(version: BriefVersion) {
  if (version.budgetUnknown || version.budgetMinor === null) return 'Not supplied'
  return version.currency
    ? formatMoney(version.budgetMinor, version.currency)
    : 'Amount supplied'
}

function campaignTypeLabel(campaignMode: CampaignMode | null) {
  if (campaignMode?.mode === masterDataCodes.campaignModes.oohOnly) {
    return 'OOH / DOOH only'
  }
  if (campaignMode?.mode === masterDataCodes.campaignModes.fullCampaign) {
    return 'Full campaign'
  }
  return 'Not resolved yet'
}

function isConfirmable(status: string) {
  return status === opportunityCodes.status.draft ||
    status === opportunityCodes.status.inReview ||
    status === masterDataCodes.lifecycleStatuses.ready
}

function useBriefRecord(tenantId: string, briefId: string) {
  const [record, setRecord] = useState<CampaignBrief | null>(null)
  const [campaignMode, setCampaignMode] = useState<CampaignMode | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const load = useCallback(async () => {
    const next = await loadBriefAndMode(tenantId, briefId)
    setRecord(next.record); setCampaignMode(next.campaignMode); setError(null)
  }, [tenantId, briefId])
  useEffect(() => { let active = true; void loadBriefAndMode(tenantId, briefId)
    .then(next => { if (active) { setRecord(next.record); setCampaignMode(next.campaignMode) } })
    .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false } }, [tenantId, briefId])
  async function confirm(version: BriefVersion, antiforgeryToken: string) {
    setBusy(true); setError(null)
    try {
      if (version.status === opportunityCodes.status.draft ||
          version.status === masterDataCodes.lifecycleStatuses.ready) {
        await briefApi.confirm(tenantId, version, antiforgeryToken)
      } else {
        await briefApi.approve(tenantId, version, antiforgeryToken)
      }
      await load()
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { record, campaignMode, error, busy, confirm }
}

async function loadBriefAndMode(tenantId: string, briefId: string) {
  const record = await briefApi.get(tenantId, briefId)
  const current = record.versions.at(-1)
  const campaignMode = current
    ? (await planningApi.getWorkspace(tenantId, current.id)).campaignMode
    : null
  return { record, campaignMode }
}
