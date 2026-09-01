import { useState, type FormEvent } from 'react'
import { campaignApi } from '../api/campaign-client'
import {
  performanceEvidenceInputSchema,
  type Campaign,
  type PerformanceEvidenceInput,
} from '../api/campaign-schemas'
import type { ProposalRecipient } from '../api/proposal-schemas'
import { Icon } from '../components/Icon'
import { masterDataCodes, masterDataDefinitions } from '../generated/master-data-codes'
import type { CampaignActionRunner } from './campaign-types'
import { MeasurementReportCard } from './MeasurementReportCard'
import { PerformanceEvidenceCard } from './PerformanceEvidenceCard'

type Props = {
  tenantId: string
  token: string
  campaign: Campaign
  reviewers: ProposalRecipient[]
  busy: boolean
  canSubmitEvidence: boolean
  canReviewEvidence: boolean
  canGenerateReport: boolean
  canReviewReport: boolean
  run: CampaignActionRunner
}

export function MeasurementSection(props: Props) {
  const available = props.campaign.status === masterDataCodes.lifecycleStatuses.completed
  return <section id="measurement-stage" className="campaign-workspace-section measurement-workspace">
    <MeasurementHeading campaign={props.campaign} />
    {!available ? <LockedMeasurement /> : <>
      {props.canSubmitEvidence && <PerformanceEvidenceForm {...props} />}
      <PerformanceEvidenceList {...props} />
      <MeasurementReportBoundary {...props} />
      <MeasurementReportList {...props} />
    </>}
  </section>
}

function LockedMeasurement() {
  return <article className="campaign-section-empty"><Icon name="chart" /><div>
    <h3>Measurement opens after delivery completes</h3>
    <p>Performance evidence can be submitted only after the booked delivery window is closed.</p>
  </div></article>
}

function MeasurementHeading({ campaign }: { campaign: Campaign }) {
  return <header className="campaign-section-heading"><div><p className="eyebrow">Performance and learning</p>
    <h2>Sourced measurement</h2><p>Canonical facts stay separate from interpretation. Method, quality and limitations remain visible in every approved report.</p></div>
    <span className="status-chip status-neutral">{campaign.performanceEvidence.length} evidence sets</span></header>
}

function PerformanceEvidenceForm(props: Props) {
  const [metricCount, setMetricCount] = useState(1)
  const [error, setError] = useState<string | null>(null)
  if (props.reviewers.length === 0) return <article className="campaign-section-empty">
    <Icon name="users" /><div><h3>A separate reviewer is required</h3>
      <p>Add an active advertiser approver before submitting performance evidence.</p></div></article>
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    const file = values.get('evidenceFile')
    const input = evidenceInput(values, metricCount)
    const parsed = performanceEvidenceInputSchema.safeParse(input)
    if (!parsed.success || !(file instanceof File) || file.size === 0) {
      setError('Complete the source, method, limitations, reviewer, metrics and evidence file.')
      return
    }
    if (file.size > 25 * 1024 * 1024) {
      setError('The performance evidence file must not exceed 25 MiB.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.submitPerformanceEvidence(
        props.tenantId, props.campaign.id, parsed.data, file, props.token),
      'The sourced performance facts were retained for independent review.',
    )
  }
  return <details className="measurement-submission" open={props.campaign.performanceEvidence.length === 0}>
    <summary>Submit sourced performance evidence</summary>
    <form onSubmit={submit}>
      {error && <p className="inline-alert" role="alert">{error}</p>}
      <EvidenceSourceFields reviewers={props.reviewers} />
      <MetricFields count={metricCount} campaign={props.campaign} />
      <div className="measurement-form-actions"><button className="secondary-button" type="button"
        onClick={() => setMetricCount(value => Math.min(20, value + 1))}>Add another metric</button>
        <label className="field-group">Evidence file
          <input name="evidenceFile" type="file" required
            accept="application/pdf,application/json,text/csv" /></label>
        <button className="primary-button" disabled={props.busy}>Submit evidence for review</button></div>
    </form>
  </details>
}

function EvidenceSourceFields({ reviewers }: { reviewers: ProposalRecipient[] }) {
  return <><div className="measurement-form-grid"><label className="field-group">Source reference
    <input name="sourceReference" required maxLength={500} /></label>
    <label className="field-group">Captured at
      <input name="capturedAtUtc" type="datetime-local" required /></label>
    <label className="field-group">Evidence quality
      <select name="qualityStatus" required defaultValue="">
        <option value="" disabled>Choose quality</option>
        {masterDataDefinitions.measurementQualityStatuses.map(item =>
          <option key={item.code} value={item.code}>{item.displayLabel}</option>)}
      </select></label>
    <label className="field-group">Assigned reviewer
      <select name="reviewerUserId" required defaultValue="">
        <option value="" disabled>Choose a different reviewer</option>
        {reviewers.map(reviewer => <option key={reviewer.userId} value={reviewer.userId}>
          {reviewer.displayName} · {reviewer.role}</option>)}
      </select></label></div>
    <label className="field-group">Methodology
      <textarea name="methodology" required maxLength={2000} rows={4} /></label>
    <label className="field-group">Limitations — one per line
      <textarea name="limitations" required maxLength={10000} rows={4} /></label></>
}

function MetricFields({ count, campaign }: { count: number; campaign: Campaign }) {
  return <div className="measurement-metric-entry">{Array.from({ length: count }, (_, index) =>
    <fieldset key={index}><legend>Metric {index + 1}</legend><div>
      <label className="field-group">Metric
        <select name={`metricType-${index}`} required defaultValue="">
          <option value="" disabled>Choose metric</option>
          {masterDataDefinitions.performanceMetricTypes.map(item =>
            <option value={item.code} key={item.code}>{item.displayLabel}</option>)}
        </select></label>
      <label className="field-group">Value
        <input name={`metricValue-${index}`} type="number" min="0" step="any" required /></label>
      <label className="field-group">Unit
        <select name={`metricUnit-${index}`} required defaultValue="">
          <option value="" disabled>Choose unit</option>
          {masterDataDefinitions.measurementUnits.map(item =>
            <option value={item.code} key={item.code}>{item.displayLabel}</option>)}
        </select></label>
      <label className="field-group">Period start
        <input name={`periodStart-${index}`} type="date" required defaultValue={campaign.startDate} /></label>
      <label className="field-group">Period end
        <input name={`periodEnd-${index}`} type="date" required defaultValue={campaign.endDate} /></label>
      <label className="field-group">Metric source locator
        <input name={`sourceLocator-${index}`} required maxLength={500} /></label>
    </div></fieldset>)}</div>
}

function PerformanceEvidenceList(props: Props) {
  if (props.campaign.performanceEvidence.length === 0) return null
  return <section className="measurement-record-list"><header><h3>Performance evidence</h3>
    <span>{props.campaign.performanceEvidence.length} retained set{props.campaign.performanceEvidence.length === 1 ? '' : 's'}</span></header>
    <div>{props.campaign.performanceEvidence.map(evidence =>
      <PerformanceEvidenceCard key={evidence.id} tenantId={props.tenantId} token={props.token}
        evidence={evidence} busy={props.busy} canReview={props.canReviewEvidence} run={props.run} />)}</div>
  </section>
}

function MeasurementReportBoundary(props: Props) {
  if (!props.canGenerateReport) return null
  const approvedProof = props.campaign.deliveryProofs.some(proof =>
    proof.status === masterDataCodes.lifecycleStatuses.approved)
  const approvedEvidence = props.campaign.performanceEvidence.some(evidence =>
    evidence.status === masterDataCodes.lifecycleStatuses.approved)
  const pendingReport = props.campaign.measurementReports.some(report => !report.reviewedBy)
  if (!approvedProof || !approvedEvidence || pendingReport) return null
  return <ReportGenerationForm {...props} />
}

function ReportGenerationForm(props: Props) {
  const [error, setError] = useState<string | null>(null)
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const approver = String(new FormData(event.currentTarget).get('approverUserId') ?? '')
    if (!approver) {
      setError('Choose a different person to review the generated report.')
      return
    }
    setError(null)
    void props.run(
      () => campaignApi.generateMeasurementReport(
        props.tenantId, props.campaign.id, approver, props.token),
      'A sourced measurement report was generated for independent human review.',
    )
  }
  return <form className="measurement-report-generation" onSubmit={submit}><div>
    <p className="eyebrow">Interpret approved facts</p><h3>Generate the client measurement report</h3>
    <p>Advertified may interpret only the approved metrics and must retain every limitation.</p></div>
    <label className="field-group">Assigned report reviewer
      <select name="approverUserId" required defaultValue="">
        <option value="" disabled>Choose a different reviewer</option>
        {props.reviewers.map(reviewer => <option value={reviewer.userId} key={reviewer.userId}>
          {reviewer.displayName} · {reviewer.role}</option>)}
      </select></label>
    {error && <p className="inline-alert" role="alert">{error}</p>}
    <button className="primary-button" disabled={props.busy}>Generate sourced report</button>
  </form>
}

function MeasurementReportList(props: Props) {
  if (props.campaign.measurementReports.length === 0) return null
  return <section className="measurement-record-list"><header><h3>Measurement reports</h3>
    <span>{props.campaign.measurementReports.length} version{props.campaign.measurementReports.length === 1 ? '' : 's'}</span></header>
    <div>{props.campaign.measurementReports.map(report =>
      <MeasurementReportCard key={report.id} tenantId={props.tenantId} token={props.token}
        report={report} busy={props.busy} canReview={props.canReviewReport} run={props.run} />)}</div>
  </section>
}

function evidenceInput(values: FormData, count: number): PerformanceEvidenceInput {
  return {
    sourceReference: String(values.get('sourceReference') ?? ''),
    capturedAtUtc: toUtc(String(values.get('capturedAtUtc') ?? '')),
    methodology: String(values.get('methodology') ?? ''),
    limitations: String(values.get('limitations') ?? '').split(/\r?\n/)
      .map(value => value.trim()).filter(Boolean),
    qualityStatus: String(values.get('qualityStatus') ?? ''),
    reviewerUserId: String(values.get('reviewerUserId') ?? ''),
    metrics: Array.from({ length: count }, (_, index) => ({
      metricType: String(values.get(`metricType-${index}`) ?? ''),
      value: Number(values.get(`metricValue-${index}`)),
      unit: String(values.get(`metricUnit-${index}`) ?? ''),
      periodStart: String(values.get(`periodStart-${index}`) ?? ''),
      periodEnd: String(values.get(`periodEnd-${index}`) ?? ''),
      sourceLocator: String(values.get(`sourceLocator-${index}`) ?? ''),
    })),
  }
}

function toUtc(value: string) {
  const parsed = new Date(value)
  return Number.isNaN(parsed.getTime()) ? value : parsed.toISOString()
}
