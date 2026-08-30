import type { MediaPlan } from '../api/planning-schemas'
import { MediaTypeIcon } from '../components/MediaTypeIcon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
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
    <div className="planning-section-heading"><div><p className="eyebrow">Media plan</p>
      <h2 id="media-plan-title">Reconciled plan</h2>
      <p>Exact selected inventory, running periods, supply confidence and commercial totals.</p></div>
      <div className="plan-total"><span>Total</span><strong>{formatMoney(plan.totalMinor, plan.currency)}</strong>
        <small>{plan.supplyConfidence.replaceAll('_', ' ')} supply confidence</small></div></div>
    <div className="plan-money-strip"><Money label="Media" amount={plan.subtotalMinor} currency={plan.currency} />
      <Money label="Fees" amount={plan.feesMinor} currency={plan.currency} />
      <Money label="VAT" amount={plan.vatMinor} currency={plan.currency} />
      <Money label="Total" amount={plan.totalMinor} currency={plan.currency} /></div>
    <div className="plan-lines">{plan.lines.map(line => {
      const visual = mediaVisual(line.channel)
      return <article className={`plan-line media-tone-${visual.tone}`} key={line.id}>
        <div className="media-identity"><MediaTypeIcon channel={line.channel} />
          <div><span>{visual.label}</span><h3>{line.name}</h3><small>{line.geography}</small></div></div>
        <div className="plan-line-periods">{line.runningPeriods.map(period =>
          <span key={`${period.start}-${period.end}`}>{formatDate(period.start)} – {formatDate(period.end)}</span>)}</div>
        <div className="plan-line-commercial"><span>Qty <strong>{line.quantity}</strong></span>
          <span>Media <strong>{formatMoney(line.supplierCostMinor, plan.currency)}</strong></span>
          <span>Client <strong>{formatMoney(line.clientPriceMinor, plan.currency)}</strong></span>
          <span>Supply <strong>{line.supplyConfidence.replaceAll('_', ' ')}</strong></span></div>
      </article>
    })}</div>
    {plan.objections.length > 0 && <div className="plan-objections"><h3>Items to review</h3>
      {plan.objections.map(item => <article key={item.code} className={item.resolution ? 'is-resolved' : ''}>
        <div><strong>{item.evidenceGap}</strong><p>{item.recommendedResolution}</p></div>
        {item.resolution ? <span className="status-chip">Reviewed</span> :
          <button className="secondary-button" type="button" disabled={busy}
            onClick={() => void onResolve(item.code)}>Review and accept</button>}
      </article>)}</div>}
    {plan.status === masterDataCodes.lifecycleStatuses.inReview && <div className="planning-actions">
      <button className="primary-button" type="button" disabled={busy || !approvable}
        onClick={() => void onApprove()}>Approve media plan</button>
    </div>}
    {plan.status === masterDataCodes.lifecycleStatuses.approved && <p className="planning-confirmed">Media plan approved and ready for proposal preparation.</p>}
  </section>
}

function Money({ label, amount, currency }: { label: string; amount: number; currency: string }) {
  return <div><span>{label}</span><strong>{formatMoney(amount, currency)}</strong></div>
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-ZA', { day: 'numeric', month: 'short', year: 'numeric' })
    .format(new Date(`${value}T00:00:00Z`))
}
