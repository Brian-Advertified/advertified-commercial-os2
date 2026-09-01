import { useState, type FormEvent } from 'react'
import { campaignApi } from '../api/campaign-client'
import type { PerformanceEvidence } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { formatDate, formatDateTime, formatMiB, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'

type Props = {
  tenantId: string
  token: string
  evidence: PerformanceEvidence
  busy: boolean
  canReview: boolean
  run: CampaignActionRunner
}

export function PerformanceEvidenceCard(props: Props) {
  const evidence = props.evidence
  return <article className="performance-evidence-card">
    <header><span><Icon name="chart" /></span><div><small>{humanizeCode(evidence.qualityStatus, true)}</small>
      <h3>{evidence.sourceReference}</h3><p>Captured {formatDateTime(evidence.capturedAtUtc)}</p></div>
      <span className={`status-chip ${evidence.reviewedBy ? statusTone(evidence.status) : 'status-warning'}`}>
        {humanizeCode(evidence.status, true)}</span></header>
    <section className="performance-method"><h4>Methodology</h4><p>{evidence.methodology}</p></section>
    <div className="performance-metric-table" role="table" aria-label="Sourced performance metrics">
      {evidence.metrics.map(metric => <div role="row" key={metric.id}>
        <span role="cell"><strong>{humanizeCode(metric.metricType, true)}</strong>
          <small>{formatDate(metric.periodStart)} – {formatDate(metric.periodEnd)}</small></span>
        <span role="cell"><strong>{metric.value.toLocaleString()}</strong>
          <small>{humanizeCode(metric.unit, true)}</small></span>
      </div>)}
    </div>
    <details className="performance-limitations"><summary>Method limitations and source details</summary>
      <ul>{evidence.limitations.map(item => <li key={item}>{item}</li>)}</ul>
      <p>{evidence.fileName} · {evidence.mediaType} · {formatMiB(evidence.sizeBytes)}</p>
    </details>
    {evidence.reviewedBy ? <div className="proof-review-result"><Icon name="shield" /><span>
      <strong>{humanizeCode(evidence.status, true)}</strong><small>{evidence.reviewReason}</small></span></div>
      : props.canReview && <EvidenceReviewForm {...props} />}
  </article>
}

function EvidenceReviewForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '').trim()
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    if (!reason || !(submitter instanceof HTMLButtonElement)) {
      setError('Record the evidence-quality review reason before deciding.')
      return
    }
    setError(null)
    const approved = submitter.value === 'approve'
    void props.run(
      () => campaignApi.reviewPerformanceEvidence(
        props.tenantId, props.evidence, approved, reason, props.token),
      approved ? 'The sourced performance facts were approved for measurement.' : 'The evidence set was rejected and remains retained.',
    )
  }
  return <form className="proof-review-form" onSubmit={submit}>
    <label className="field-group">Evidence review reason
      <textarea name="reason" required maxLength={1000} rows={3} /></label>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div><button className="secondary-button" value="reject" disabled={props.busy}>Reject evidence</button>
      <button className="primary-button" value="approve" disabled={props.busy}>Approve evidence</button></div>
  </form>
}

function statusTone(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('approved')) return 'status-positive'
  if (normalized.includes('rejected')) return 'status-danger'
  return 'status-neutral'
}
