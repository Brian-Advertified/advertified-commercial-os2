import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { z } from 'zod'
import { humanMessage } from '../api/client'
import { proposalApi } from '../api/proposal-client'
import {
  proposalDraftInputSchema,
  type ApprovedPlanChoice,
  type ProposalDraftInput,
} from '../api/proposal-schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { LoadingState, MessageState } from '../components/PageState'
import { mediaVisual } from '../planning/media-visuals'
import { formatMoney } from '../presentation/format'
import { proposalPolicy } from '../proposal/proposal-policy'

const maximumChoices = proposalPolicy.maximumOptions

type ChoiceDraft = { plan: ApprovedPlanChoice; label: string; outcome: string }
type BuilderContext = { tenantId: string; briefId: string; token: string }

export function NewProposalPage() {
  const route = z.guid().safeParse(useParams().briefId)
  const { selected, loading } = useWorkspace()
  const { session } = useSession()
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!session || !route.success) return <Navigate to="/home" replace />
  return <ProposalBuilder tenantId={selected.tenantId} briefId={route.data}
    token={session.antiforgeryToken} />
}

function ProposalBuilder(context: BuilderContext) {
  const state = useProposalBuilder(context)
  if (state.error && !state.plans) {
    return <MessageState title="Proposal choices could not be opened" message={state.error} />
  }
  if (!state.plans) return <LoadingState label="Loading approved media plans" />
  return <BuilderContent {...context} {...state} plans={state.plans} />
}

function useProposalBuilder({ tenantId, briefId, token }: BuilderContext) {
  const navigate = useNavigate()
  const [plans, setPlans] = useState<ApprovedPlanChoice[] | null>(null)
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
  function toggle(plan: ApprovedPlanChoice) {
    setChoices(current => toggleChoice(current, plan))
  }
  function update(planId: string, patch: Partial<Pick<ChoiceDraft, 'label' | 'outcome'>>) {
    setChoices(current => current.map(item => item.plan.id === planId ? { ...item, ...patch } : item))
  }
  async function submit(input: ProposalDraftInput) {
    setBusy(true); setError(null)
    try {
      const proposal = await proposalApi.generate(tenantId, briefId, input, token)
      navigate(`/proposals/${proposal.id}`)
    } catch (failure) { setError(humanMessage(failure)) }
    finally { setBusy(false) }
  }
  return { plans, choices, error, busy, toggle, update, submit, reportError: setError }
}

type BuilderState = ReturnType<typeof useProposalBuilder> & { plans: ApprovedPlanChoice[] }

function BuilderContent({ briefId, plans, choices, error, busy, toggle, update, submit, reportError }: BuilderContext & BuilderState) {
  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const parsed = proposalDraftInputSchema.safeParse(buildInput(new FormData(event.currentTarget), choices))
    if (!parsed.success) {
      reportError(parsed.error.issues[0]?.message ?? 'Review the proposal choices and try again.')
      return
    }
    await submit(parsed.data)
  }
  return <section className="proposal-page proposal-builder" aria-labelledby="proposal-builder-title">
    <Link className="text-action back-link" to={`/briefs/${briefId}`}>← Back to Brief</Link>
    <BuilderHero choiceCount={choices.length} />
    {error && <p className="inline-alert" role="alert">{error}</p>}
    {plans.length === 0 ? <EmptyPlans briefId={briefId} /> :
      <form onSubmit={event => void handleSubmit(event)} className="proposal-builder-form">
        <PlanSelectionSection plans={plans} choices={choices} onToggle={toggle} />
        {choices.length > 0 && <ChoiceWordingSection choices={choices} busy={busy} onUpdate={update} />}
      </form>}
  </section>
}

function BuilderHero({ choiceCount }: { choiceCount: number }) {
  return <header className="proposal-hero"><div><p className="eyebrow eyebrow-light">Client proposal</p>
    <h1 id="proposal-builder-title">Build clear choices from approved plans</h1>
    <p>Select up to three genuinely different media plans. Each choice keeps its exact budget, inventory and running periods.</p></div>
    <div className="proposal-choice-count"><strong>{choiceCount}</strong><span>of {maximumChoices} choices</span></div>
  </header>
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
  return <section className="proposal-section" aria-labelledby="approved-plans-title">
    <div className="proposal-section-heading"><div><p className="eyebrow">Approved planning</p>
      <h2 id="approved-plans-title">Choose the routes to present</h2>
      <p>Plan budgets are fixed here. Change planning first when a budget, channel or placement must change.</p></div></div>
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

function toggleChoice(current: ChoiceDraft[], plan: ApprovedPlanChoice) {
  if (current.some(item => item.plan.id === plan.id)) return current.filter(item => item.plan.id !== plan.id)
  if (current.length >= maximumChoices) return current
  return [...current, defaultChoice(plan, current.length + 1)]
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

function defaultChoice(plan: ApprovedPlanChoice, ordinal: number): ChoiceDraft {
  const labels = plan.channels.map(channel => mediaVisual(channel).label)
  return { plan, label: labels.length === 1 ? `${labels[0]} route` : `Integrated route ${ordinal}`,
    outcome: `Use ${labels.join(' and ')} to deliver the approved campaign outcome.` }
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
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short' })
    .format(new Date(`${value}T00:00:00`))
}
