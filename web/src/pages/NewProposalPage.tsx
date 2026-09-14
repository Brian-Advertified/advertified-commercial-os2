import { useEffect, useState } from 'react'
import { Link, Navigate, useLocation, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { inventoryApi } from '../api/inventory-client'
import { planningApi } from '../api/planning-client'
import type { PlanningWorkspace } from '../api/planning-schemas'
import { proposalApi } from '../api/proposal-client'
import {
  proposalDraftInputSchema,
  type ApprovedPlanChoice,
  type ProposalDraftInput,
} from '../api/proposal-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import {
  BriefFlowBinding,
  BriefVersionFlowBinding,
} from '../campaign-flow/CampaignFlowBindings'
import { Icon } from '../components/Icon'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'
import { PlanningDecisionContext } from '../planning/PlanningDecisionContext'
import { mediaVisual } from '../planning/media-visuals'
import { formatDate, formatMoney } from '../presentation/format'
import { proposalPolicy } from '../proposal/proposal-policy'

const maximumChoices = proposalPolicy.maximumOptions

type ChoiceDraft = { plan: ApprovedPlanChoice; label: string; outcome: string }
type BuilderContext = { tenantId: string; briefId: string; token: string; replacesProposalId: string | null }

export function NewProposalPage() {
  const route = z.guid().safeParse(useParams().briefId)
  const location = useLocation()
  const replacesValue = new URLSearchParams(location.search).get('replaces')
  const replacesProposalId = replacesValue && z.guid().safeParse(replacesValue).success ? replacesValue : null
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/home" replace />
  return <ProposalBuilder tenantId={selected.tenantId} briefId={route.data}
    token={session.antiforgeryToken} replacesProposalId={replacesProposalId} />
}

function ProposalBuilder(context: BuilderContext) {
  const state = useProposalBuilder(context)
  if (state.error && !state.plans) {
    return <MessageState title="Proposal choices could not be opened" message={state.error} />
  }
  if (!state.plans) return <LoadingState label="Loading approved media plans" />
  const versionId = state.plans[0]?.briefVersionId
  return <>{versionId
      ? <BriefVersionFlowBinding tenantId={context.tenantId} briefVersionId={versionId} />
      : <BriefFlowBinding tenantId={context.tenantId} briefId={context.briefId} />}
    <BuilderContent {...context} {...state} plans={state.plans} /></>
}

function useProposalBuilder({ tenantId, briefId, token, replacesProposalId }: BuilderContext) {
  const navigate = useNavigate()
  const [plans, setPlans] = useState<ApprovedPlanChoice[] | null>(null)
  const [planning, setPlanning] = useState<PlanningWorkspace | null>(null)
  const [choices, setChoices] = useState<ChoiceDraft[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    let active = true
    void proposalApi.listApprovedPlans(tenantId, briefId)
      .then(value => { if (active) setPlans(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [tenantId, briefId])
  useEffect(() => {
    const briefVersionId = plans?.[0]?.briefVersionId
    if (!briefVersionId) return
    let active = true
    void planningApi.getWorkspace(tenantId, briefVersionId)
      .then(value => { if (active) setPlanning(value) })
      .catch(() => { if (active) setPlanning(null) })
    return () => { active = false }
  }, [tenantId, plans])
  function toggle(plan: ApprovedPlanChoice) {
    setChoices(current => toggleChoice(
      current, plan, planning?.decisionContext?.objective ?? null))
  }
  function update(planId: string, patch: Partial<Pick<ChoiceDraft, 'label' | 'outcome'>>) {
    setChoices(current => current.map(item => item.plan.id === planId ? { ...item, ...patch } : item))
  }
  async function submit(input: ProposalDraftInput) {
    setBusy(true); setError(null)
    try {
      const proposal = await proposalApi.generate(tenantId, briefId, input, token)
      if (replacesProposalId) {
        try {
          const impacts = await inventoryApi.proposalInventoryImpacts(tenantId, replacesProposalId)
          const open = impacts.filter(item => item.status === masterDataCodes.proposalInventoryImpactStatuses.open)
          for (const impact of open) {
            await inventoryApi.resolveProposalInventoryImpact(
              tenantId,
              impact,
              proposal.id,
              'Replacement proposal reviewed against current inventory and reconfirmed by the planner.',
              token,
            )
          }
          if (open.length > 0) notifications.success('The proposal revision is current and the previous inventory impacts are resolved.')
        } catch (failure) {
          notifications.warning(`The proposal revision was created, but the previous inventory review is still open: ${humanMessage(failure)}`)
        }
      }
      navigate(`/proposals/${proposal.id}`)
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { plans, planning, choices, error, busy, toggle, update, submit, reportError: setError }
}

type BuilderState = ReturnType<typeof useProposalBuilder> & { plans: ApprovedPlanChoice[] }

function BuilderContent({ briefId, replacesProposalId, plans, planning, choices, error, busy, toggle, update, submit, reportError }: BuilderContext & BuilderState) {
  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = proposalDraftInputSchema.safeParse(buildInput(new FormData(event.currentTarget), choices))
    if (!parsed.success) {
      reportError(parsed.error.issues[0]?.message ?? 'Review the proposal choices and try again.')
      return
    }
    await submit(parsed.data)
  }
  return <section className="proposal-page proposal-builder connected-proposal-builder" aria-labelledby="proposal-builder-title">
    <header className="connected-stage-heading"><div><p className="eyebrow">New campaign</p>
      <h1 id="proposal-builder-title">Proposal builder</h1>
      <p>Turn the approved strategy and inventory into clear, client-ready proposal choices.</p></div>
      <div className="connected-handwritten-note" aria-hidden="true">Ideas today.<br />Impact tomorrow.<span /></div></header>
    {replacesProposalId && <section className="connected-revision-banner"><Icon name="shield" /><div>
      <strong>Revision & reconfirmation</strong><p>This proposal will replace an inventory-affected proposal version. Advertified will resolve the previous inventory-review impacts only after this new version is created from current approved planning.</p></div></section>}
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {plans.length === 0 ? <EmptyPlans briefId={briefId} /> :
      <form onSubmit={event => void handleSubmit(event)} className="proposal-builder-form connected-proposal-form">
        <div className="connected-proposal-left">
          <PlanSelectionSection plans={plans} choices={choices} onToggle={toggle} />
          {choices.length > 0 && <ChoiceWordingSection choices={choices} busy={busy} onUpdate={update} />}
          <Link className="secondary-button connected-proposal-back" to={plans[0]?.briefVersionId
            ? `/planning/${plans[0].briefVersionId}` : `/briefs/${briefId}`}>← Back: Media Plan</Link>
        </div>
        <ConnectedProposalPreview choices={choices} planning={planning} planCount={plans.length} />
      </form>}
    {planning?.decisionContext && <details className="connected-proposal-context"><summary>View approved planning context</summary>
      <PlanningDecisionContext value={planning.decisionContext} /></details>}
  </section>
}

function ConnectedProposalPreview({ choices, planning, planCount }: {
  choices: ChoiceDraft[]
  planning: PlanningWorkspace | null
  planCount: number
}) {
  const model = proposalPreviewModel(choices, planning)
  return <aside className="connected-proposal-preview"><header><div><IconPreview />
    <h2>Proposal preview</h2></div><span>{choices.length}/{Math.min(planCount, maximumChoices)} selected</span></header>
    <ProposalCover {...model} />
    <ProposalPreviewHighlights {...model} />
  </aside>
}

function ProposalCover({ title, clientName, channels }: ReturnType<typeof proposalPreviewModel>) {
  return <div className="connected-proposal-cover"><img src="/advertified-wordmark.png" alt="Advertified" />
    <small>CAMPAIGN PROPOSAL</small><h3>{title}</h3><p>{clientName}</p>
    <div className="connected-proposal-cover-image" aria-hidden="true" />
    <footer>{channels.length ? channels.map(channel => <span key={channel}><MediaTypeIcon channel={channel} />
      {mediaVisual(channel).label}</span>) : <span>Approved channels appear here</span>}</footer>
  </div>
}

function ProposalPreviewHighlights({ investment, objective, audience }: ReturnType<typeof proposalPreviewModel>) {
  return <section className="connected-proposal-highlights"><header><h3>Client-ready highlights</h3>
    <span>From approved plan</span></header><div>
      <article><strong>Approved investment</strong><p>{investment}</p></article>
      <article><strong>Campaign objective</strong><p>{objective}</p></article>
      <article><strong>Audience direction</strong><p>{audience}</p></article>
      <article><strong>Evidence</strong><p>Pricing, channels and periods remain bound to the approved media plan.</p></article>
    </div></section>
}

function proposalPreviewModel(choices: ChoiceDraft[], planning: PlanningWorkspace | null) {
  const selected = choices[0]
  return {
    title: selectedTitle(selected),
    clientName: planningText(planning?.clientName, 'Client proposal'),
    channels: selectedChannels(selected),
    investment: selectedInvestment(selected),
    objective: planningText(planning?.decisionContext?.objective, 'Approved Brief objective'),
    audience: planningText(planning?.decisionContext?.targetingRationale, 'Approved audience strategy'),
  }
}

function selectedTitle(selected?: ChoiceDraft) { return selected?.label ?? 'Select a proposal route' }
function selectedChannels(selected?: ChoiceDraft) { return selected?.plan.channels ?? [] }
function selectedInvestment(selected?: ChoiceDraft) {
  return selected ? formatMoney(selected.plan.totalMinor, selected.plan.currency) : 'Select a plan'
}
function planningText(value: string | null | undefined, fallback: string) { return value || fallback }

function IconPreview() {
  return <span className="connected-preview-eye" aria-hidden="true">◉</span>
}

function EmptyPlans({ briefId }: { briefId: string }) {
  return <article className="detail-card proposal-empty"><h2>No approved plans yet</h2>
    <p>Approve at least one media plan before preparing the client proposal.</p>
    <Link className="primary-button" to={`/briefs/${briefId}`}>Return to the Brief</Link></article>
}

function PlanSelectionSection({ plans, choices, onToggle }: {
  plans: ApprovedPlanChoice[]
  choices: ChoiceDraft[]
  onToggle: (plan: ApprovedPlanChoice) => void
}) {
  const selected = new Set(choices.map(item => item.plan.id))
  return <section className="proposal-section connected-package-section" aria-labelledby="approved-plans-title">
    <div className="proposal-section-heading"><div>
      <h2 id="approved-plans-title">Campaign packages</h2>
      <p>Create one or more package options for your client from approved media plans.</p></div>
      <span>{plans.length} approved plan{plans.length === 1 ? '' : 's'}</span></div>
    <div className="approved-plan-grid">{plans.map(plan => <PlanChoiceCard key={plan.id}
      plan={plan} selected={selected.has(plan.id)}
      disabled={choices.length >= maximumChoices && !selected.has(plan.id)}
      onToggle={() => onToggle(plan)} />)}</div>
  </section>
}

function ChoiceWordingSection({ choices, busy, onUpdate }: {
  choices: ChoiceDraft[]
  busy: boolean
  onUpdate: (planId: string, patch: Partial<Pick<ChoiceDraft, 'label' | 'outcome'>>) => void
}) {
  return <section className="proposal-section" aria-labelledby="proposal-wording-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Client wording</p>
      <h2 id="proposal-wording-title">Explain the value of each route</h2>
      <p>Use outcome-led language. The approved plan remains the commercial source of truth.</p></div></div>
    <div className="proposal-choice-editors">{choices.map((choice, index) =>
      <ChoiceEditor key={choice.plan.id} choice={choice} index={index}
        onUpdate={patch => onUpdate(choice.plan.id, patch)} />)}</div>
    <ProposalDetails busy={busy} />
  </section>
}

function PlanChoiceCard({ plan, selected, disabled, onToggle }: {
  plan: ApprovedPlanChoice; selected: boolean; disabled: boolean; onToggle: () => void
}) {
  return <button type="button" className={`approved-plan-card ${selected ? 'is-selected' : ''}`}
    aria-pressed={selected} disabled={disabled} onClick={onToggle}>
    <div className="approved-plan-head"><div><span>Plan {plan.versionNumber}</span>
      <strong>{formatMoney(plan.totalMinor, plan.currency)}</strong></div>
      <span className="plan-choice-indicator">{selected ? 'Selected' : 'Select'}</span></div>
    <div className="proposal-media-icons">{plan.channels.map(channel =>
      <span key={channel} title={mediaVisual(channel).label}><MediaTypeIcon channel={channel} /></span>)}</div>
    <p>{plan.channels.map(channel => mediaVisual(channel).label).join(' · ')}</p>
    <small>{formatPeriodSummary(plan)}</small>
  </button>
}

function ChoiceEditor({ choice, index, onUpdate }: {
  choice: ChoiceDraft; index: number
  onUpdate: (patch: Partial<Pick<ChoiceDraft, 'label' | 'outcome'>>) => void
}) {
  return <article className="proposal-choice-editor"><div className="proposal-choice-number">{index + 1}</div>
    <div className="proposal-choice-fields">
      <label className="field-group">Choice name<input value={choice.label} required maxLength={200}
        onChange={event => onUpdate({ label: event.target.value })} /></label>
      <label className="field-group field-wide">Client outcome<textarea value={choice.outcome} required maxLength={2000}
        onChange={event => onUpdate({ outcome: event.target.value })} /></label>
      <div className="proposal-plan-lock"><span>Approved plan</span>
        <strong>{formatMoney(choice.plan.totalMinor, choice.plan.currency)}</strong>
        <small>{choice.plan.channels.map(channel => mediaVisual(channel).label).join(', ')}</small></div>
    </div>
  </article>
}

function ProposalDetails({ busy }: { busy: boolean }) {
  return <div className="proposal-details-grid">
    <label className="field-group field-wide">Proposal title
      <input name="title" required maxLength={300} defaultValue="Media proposal" /></label>
    <label className="field-group">Valid until
      <input name="expiry" type="date" required defaultValue={defaultExpiry()} /></label>
    <label className="field-group field-wide">Commercial terms
      <textarea name="terms" required maxLength={10_000}
        defaultValue="Rates and availability remain subject to the approved plan evidence and stated validity. Final booking follows client selection and supplier confirmation." /></label>
    <div className="proposal-submit-row"><p>The next screen lets you refine the executive summary before approval.</p>
      <button className="primary-button" type="submit" disabled={busy}>
        {busy ? 'Creating proposal…' : 'Create proposal'}
      </button></div>
  </div>
}

function toggleChoice(current: ChoiceDraft[], plan: ApprovedPlanChoice, objective: string | null) {
  if (current.some(item => item.plan.id === plan.id)) return current.filter(item => item.plan.id !== plan.id)
  if (current.length >= maximumChoices) return current
  return [...current, defaultChoice(plan, current.length + 1, objective)]
}

function buildInput(form: FormData, choices: ChoiceDraft[]): ProposalDraftInput {
  const expiry = String(form.get('expiry') ?? '')
  return {
    title: String(form.get('title') ?? '').trim(),
    terms: String(form.get('terms') ?? '').trim(),
    expiryAtUtc: new Date(`${expiry}T23:59:59`).toISOString(),
    options: choices.map(choice => ({
      planVersionId: choice.plan.id,
      label: choice.label.trim(),
      outcome: choice.outcome.trim(),
    })),
  }
}

function defaultChoice(plan: ApprovedPlanChoice, ordinal: number, objective: string | null): ChoiceDraft {
  const labels = plan.channels.map(channel => mediaVisual(channel).label)
  const channelLabel = labels.length > 0 ? labels.join(' + ') : `Plan ${ordinal}`
  const objectiveCopy = objective?.trim()
    ? `to support the approved objective: ${objective.trim()}`
    : 'against the approved campaign objective'
  return {
    plan,
    label: labels.length === 1 ? `${channelLabel} focused plan` : `${channelLabel} integrated plan`,
    outcome: `Invest ${formatMoney(plan.totalMinor, plan.currency)} across ${labels.join(' and ') || 'the approved media plan'} ${objectiveCopy}, using the retained inventory and running periods.`,
  }
}

function defaultExpiry() {
  const date = new Date()
  date.setDate(date.getDate() + proposalPolicy.defaultValidityDays)
  return date.toISOString().slice(0, 10)
}

function formatPeriodSummary(plan: ApprovedPlanChoice) {
  const periods = plan.runningPeriods.map(period => `${shortDate(period.start)}–${shortDate(period.end)}`)
  return periods.length ? [...new Set(periods)].join(' · ') : 'Running periods retained in the plan'
}

function shortDate(value: string) {
  return formatDate(value)
}
