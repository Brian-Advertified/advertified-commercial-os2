import type { OpportunityDetail } from '../api/schemas'
import { formatDate, formatMoney } from '../presentation/format'

type Check = { label: string; value: string; ready: boolean }

export function OpportunityQualification({ detail }: { detail: OpportunityDetail }) {
  const checks = qualificationChecks(detail)
  const complete = checks.every(item => item.ready)
  return <section className="operations-panel operations-full-panel" aria-labelledby="commercial-qualification-title">
    <header className="operations-panel-header"><div><p className="eyebrow">Commercial qualification</p>
      <h2 id="commercial-qualification-title">Is this enquiry framed well enough to progress?</h2></div>
      <span className={`status-chip ${complete ? 'status-positive' : 'status-warning'}`}>
        {complete ? 'Commercial framing complete' : 'Qualification gaps remain'}</span></header>
    <div className="operations-register">{checks.map(item => <QualificationRow key={item.label} item={item} />)}</div>
    <p className="operations-panel-footer">This is a commercial-framing check only. The governed next action above still controls whether evidence, interpretation, strategy or the canonical Brief must happen next.</p>
  </section>
}

function qualificationChecks(detail: OpportunityDetail): Check[] {
  const item = detail.opportunity
  return [
    check('Business problem', item.problemSummary),
    check('Campaign objective', item.objectiveSummary),
    moneyCheck(item.expectedValueMinor, item.currency),
    deadlineCheck(item.deadline),
    evidenceCheck(detail),
  ]
}

function check(label: string, value: string | null) {
  const ready = Boolean(value?.trim())
  return { label, value: ready ? value!.trim() : 'Not established', ready }
}

function moneyCheck(amount: number | null, currency: string | null): Check {
  const ready = amount !== null && Boolean(currency)
  return { label: 'Expected value', value: ready ? formatMoney(amount!, currency!, 0) : 'Not established', ready }
}

function deadlineCheck(deadline: string | null): Check {
  return { label: 'Decision deadline', value: deadline ? formatDate(deadline) : 'Not established', ready: deadline !== null }
}

function evidenceCheck(detail: OpportunityDetail): Check {
  const ready = detail.sources.length > 0 && detail.evidenceItems.length > 0
  const value = ready
    ? `${detail.evidenceItems.length} reviewed claim(s) from ${detail.sources.length} source(s)`
    : 'Evidence still required'
  return { label: 'Retained evidence', value, ready }
}

function QualificationRow({ item }: { item: Check }) {
  return <div className="operations-register-row">
    <span><strong>{item.label}</strong><small>{item.value}</small></span>
    <span><small>Status</small>{item.ready ? 'Established' : 'Needs attention'}</span>
  </div>
}
