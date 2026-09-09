import type { MediaPlan } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDate, formatMoney } from '../presentation/format'
import { mediaVisual } from './media-visuals'

export function MediaPlanPanel({ plan, busy, onResolve, onApprove }: {
  plan: MediaPlan
  busy: boolean
  onResolve: (code: string) => Promise<void>
  onApprove: () => Promise<void>
}) {
  const unresolved = plan.objections.filter(item => item.resolution === null)
  const approvable = plan.status === masterDataCodes.lifecycleStatuses.inReview && unresolved.length === 0
  return <section className="planning-section" aria-labelledby="media-plan-title">
    <PlanHeading plan={plan} />
    <CommercialWaterfall plan={plan} />
    <PlanLines plan={plan} />
    <Objections plan={plan} busy={busy} onResolve={onResolve} />
    {plan.status === masterDataCodes.lifecycleStatuses.inReview && <div className="planning-actions">
      <button className="primary-button" type="button" disabled={busy || !approvable}
        onClick={() => void onApprove()}>Approve media plan</button>
    </div>}
    {plan.status === masterDataCodes.lifecycleStatuses.approved &&
      <p className="planning-confirmed">Media plan approved and ready for proposal preparation.</p>}
  </section>
}

function PlanHeading({ plan }: { plan: MediaPlan }) {
  return <div className="planning-section-heading"><div><p className="eyebrow">Media plan</p>
    <h2 id="media-plan-title">Reconciled plan</h2>
    <p>Exact selected inventory, running periods, supply confidence and commercial totals.</p></div>
    <div className="plan-total"><span>Total</span><strong>{formatMoney(plan.totalMinor, plan.currency)}</strong>
      <small>{plan.supplyConfidence.replaceAll('_', ' ')} supply confidence</small></div></div>
}

function CommercialWaterfall({ plan }: { plan: MediaPlan }) {
  const supplier = Math.max(0, plan.totalMinor - plan.feesMinor - plan.vatMinor)
  const execution = executionCosts(plan)
  return <><div className="plan-money-strip">
    <Money label="Supplier media & execution" amount={supplier} currency={plan.currency} />
    <Money label="Commercial fees" amount={plan.feesMinor} currency={plan.currency} />
    <Money label="VAT" amount={plan.vatMinor} currency={plan.currency} />
    <Money label="Client total" amount={plan.totalMinor} currency={plan.currency} />
  </div>
  {execution && <p className="planning-commercial-note">Supplied execution terms already included in supplier pricing: {execution}</p>}</>
}

function executionCosts(plan: MediaPlan) {
  const production = plan.lines.reduce((sum, line) => sum + (line.commercialTerms?.productionCostMinor ?? 0), 0)
  const installation = plan.lines.reduce((sum, line) => sum + (line.commercialTerms?.installationCostMinor ?? 0), 0)
  const values = [
    production > 0 ? `production ${formatMoney(production, plan.currency)}` : '',
    installation > 0 ? `installation ${formatMoney(installation, plan.currency)}` : '',
  ].filter(Boolean)
  return values.length > 0 ? values.join(' · ') : null
}

function PlanLines({ plan }: { plan: MediaPlan }) {
  return <div className="plan-lines">{plan.lines.map(line => <PlanLine key={line.id} line={line} currency={plan.currency} />)}</div>
}

function PlanLine({ line, currency }: { line: MediaPlan['lines'][number]; currency: string }) {
  const visual = mediaVisual(line.channel)
  return <article className={`plan-line media-tone-${visual.tone}`}>
    <div className="media-identity"><MediaTypeIcon channel={line.channel} />
      <div><span>{visual.label}</span><h3 title={line.name}>{line.name}</h3><small>{line.geography}</small></div></div>
    <div className="plan-line-periods">{line.runningPeriods.map(period =>
      <span key={`${period.start}-${period.end}`}>{formatDate(period.start)} – {formatDate(period.end)}</span>)}</div>
    <div className="plan-line-commercial"><span>Qty <strong>{line.quantity}</strong>{purchaseBasis(line)}</span>
      <span>Client price <strong>{formatMoney(line.clientPriceMinor, currency)}</strong></span>
      <span>Supply <strong>{line.supplyConfidence.replaceAll('_', ' ')}</strong></span></div>
  </article>
}

function purchaseBasis(line: MediaPlan['lines'][number]) {
  return line.purchase ? ` (${line.purchase.rateType}; rate per ${line.purchase.denominator ?? 'unspecified'})` : ''
}

function Objections({ plan, busy, onResolve }: {
  plan: MediaPlan; busy: boolean; onResolve: (code: string) => Promise<void>
}) {
  if (plan.objections.length === 0) return null
  return <div className="plan-objections"><h3>Items to review</h3>{plan.objections.map(item =>
    <article key={item.code} className={item.resolution ? 'is-resolved' : ''}>
      <div><strong>{item.evidenceGap}</strong><p>{item.recommendedResolution}</p></div>
      {item.resolution ? <span className="status-chip">Reviewed</span> :
        <button className="secondary-button" type="button" disabled={busy}
          onClick={() => void onResolve(item.code)}>Review and accept</button>}
    </article>)}</div>
}

function Money({ label, amount, currency }: { label: string; amount: number; currency: string }) {
  return <div><span>{label}</span><strong>{formatMoney(amount, currency)}</strong></div>
}
