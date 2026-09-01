import { useState, type FormEvent } from 'react'
import { campaignApi } from '../api/campaign-client'
import type { MeasurementReport } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { formatDateTime, humanizeCode } from '../presentation/format'
import type { CampaignActionRunner } from './campaign-types'

type Props = {
  tenantId: string
  token: string
  report: MeasurementReport
  busy: boolean
  canReview: boolean
  run: CampaignActionRunner
}

export function MeasurementReportCard(props: Props) {
  const report = props.report
  return <article className="measurement-report-card">
    <header><span><Icon name="chart" /></span><div><small>Report version {report.versionNumber}</small>
      <h3>{report.interpretation.executiveSummary}</h3>
      <p>Generated {formatDateTime(report.generatedAtUtc)}</p></div>
      <span className={`status-chip ${report.reviewedBy ? statusTone(report.status) : 'status-warning'}`}>
        {humanizeCode(report.status, true)}</span></header>
    <section className="measurement-findings"><h4>Evidence-backed findings</h4>
      {report.interpretation.findings.map((finding, index) => <article key={`${finding.title}-${index}`}>
        <div><strong>{finding.title}</strong><span>{humanizeCode(finding.causalityStatus, true)}</span></div>
        <p>{finding.summary}</p><small>{finding.metricIds.length} sourced metric reference{finding.metricIds.length === 1 ? '' : 's'}</small>
      </article>)}</section>
    <div className="measurement-report-columns"><section><h4>Limitations</h4>
      <ul>{report.interpretation.limitations.map(item => <li key={item}>{item}</li>)}</ul></section>
      <section><h4>Learning proposals</h4>
        <ul>{report.interpretation.learningProposals.map((item, index) =>
          <li key={`${item.text}-${index}`}>{item.text}{item.requiresNewApproval &&
            <small>Requires a separate human-approved future action.</small>}</li>)}</ul></section></div>
    {report.reviewedBy ? <div className="proof-review-result"><Icon name="shield" /><span>
      <strong>{humanizeCode(report.status, true)}</strong><small>{report.reviewReason}</small></span></div>
      : props.canReview && <ReportReviewForm {...props} />}
  </article>
}

function ReportReviewForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '').trim()
    const submitter = (event.nativeEvent as SubmitEvent).submitter
    if (!reason || !(submitter instanceof HTMLButtonElement)) {
      setError('Record the report review reason before deciding.')
      return
    }
    setError(null)
    const approved = submitter.value === 'approve'
    void props.run(
      () => campaignApi.reviewMeasurementReport(
        props.tenantId, props.report, approved, reason, props.token),
      approved ? 'The sourced measurement report was approved for client viewing.' : 'The report was rejected and remains retained.',
    )
  }
  return <form className="proof-review-form" onSubmit={submit}>
    <label className="field-group">Report review reason
      <textarea name="reason" required maxLength={1000} rows={3} /></label>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <div><button className="secondary-button" value="reject" disabled={props.busy}>Reject report</button>
      <button className="primary-button" value="approve" disabled={props.busy}>Approve client report</button></div>
  </form>
}

function statusTone(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('approved')) return 'status-positive'
  if (normalized.includes('rejected')) return 'status-danger'
  return 'status-neutral'
}
