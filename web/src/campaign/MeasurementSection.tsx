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
import { humanizeCode } from '../presentation/format'
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
  const [view, setView] = useState<'reporting' | 'learning'>('reporting')
  const available = props.campaign.status === masterDataCodes.lifecycleStatuses.completed
  return <section id="measurement-stage" className="campaign-workspace-section measurement-workspace connected-measurement-page">
    <nav className="connected-measurement-tabs" aria-label="Reporting views">
      <button type="button" className={view === 'reporting' ? 'is-active' : ''}
        onClick={() => setView('reporting')}>Campaign reporting</button>
      <button type="button" className={view === 'learning' ? 'is-active' : ''}
        onClick={() => setView('learning')}>Learning &amp; insights</button>
    </nav>
    {!available ? <LockedMeasurement /> : <>
      {view === 'reporting'
        ? <CampaignReportingDashboard campaign={props.campaign} />
        : <CampaignLearningDashboard campaign={props.campaign} />}
      <details className="connected-measurement-governance"><summary>Evidence, review and report governance</summary>
        {props.canSubmitEvidence && <PerformanceEvidenceForm {...props} />}
        <PerformanceEvidenceList {...props} />
        <MeasurementReportBoundary {...props} />
        <MeasurementReportList {...props} />
      </details>
    </>}
  </section>
}

function CampaignReportingDashboard({ campaign }: { campaign: Campaign }) {
  const metrics = campaign.performanceEvidence.flatMap(evidence => evidence.metrics.map(metric => ({
    ...metric,
    sourceReference: evidence.sourceReference,
    methodology: evidence.methodology,
    qualityStatus: evidence.qualityStatus,
  })))
  const metricGroups = groupMetrics(metrics)
  return <div className="connected-reporting-dashboard">
    <section className="connected-reporting-kpis">
      <ReportingKpi label="Evidence sets" value={String(campaign.performanceEvidence.length)} detail="Retained performance sources" icon="evidence" />
      <ReportingKpi label="Measured metrics" value={String(metrics.length)} detail="Sourced metric records" icon="chart" />
      <ReportingKpi label="Approved proof" value={String(campaign.deliveryProofs.filter(item =>
        item.status === masterDataCodes.lifecycleStatuses.approved).length)} detail="Reviewed delivery proof" icon="shield" />
      <ReportingKpi label="Reports" value={String(campaign.measurementReports.length)} detail="Generated report versions" icon="brief" />
    </section>
    <div className="connected-reporting-grid">
      <section className="connected-reporting-chart-card"><header><div><Icon name="chart" /><h2>Measured campaign metrics</h2></div>
        <span>{metricGroups.length} metric type{metricGroups.length === 1 ? '' : 's'}</span></header>
        {metricGroups.length ? <div className="connected-metric-bars">{metricGroups.slice(0, 8).map(group => <div key={group.type}>
          <span>{humanizeCode(group.type, true)}</span><i><b style={{ width: `${Math.max(8, group.relative)}%` }} /></i>
          <strong>{group.display}</strong></div>)}</div> : <ReportingEmpty copy="No reviewed performance metrics have been retained yet." />}
      </section>
      <section className="connected-reporting-source-card"><header><Icon name="evidence" /><h2>Measurement sources</h2></header>
        {campaign.performanceEvidence.length ? <ul>{campaign.performanceEvidence.slice(0, 6).map(evidence => <li key={evidence.id}>
          <strong>{evidence.sourceReference}</strong><span>{humanizeCode(evidence.qualityStatus, true)}</span>
          <small>{evidence.methodology}</small></li>)}</ul> : <ReportingEmpty copy="No performance evidence source is retained yet." />}
      </section>
      <section className="connected-reporting-detail-card"><header><Icon name="target" /><h2>Campaign results</h2></header>
        <dl><div><dt>Campaign status</dt><dd>{humanizeCode(campaign.status, true)}</dd></div>
          <div><dt>Delivery proof records</dt><dd>{campaign.deliveryProofs.length}</dd></div>
          <div><dt>Evidence sets</dt><dd>{campaign.performanceEvidence.length}</dd></div>
          <div><dt>Measurement reports</dt><dd>{campaign.measurementReports.length}</dd></div></dl>
        <p>Advertified does not infer reach, ROI, frequency or uplift unless those values are present in approved performance evidence.</p>
      </section>
    </div>
  </div>
}

function CampaignLearningDashboard({ campaign }: { campaign: Campaign }) {
  const report = [...campaign.measurementReports]
    .sort((a, b) => b.versionNumber - a.versionNumber)[0] ?? null
  if (!report) return <section className="connected-learning-empty"><span className="connected-ai-orb">✦</span><div>
    <h2>Learning &amp; insights will appear after a measurement report is generated</h2>
    <p>Advertified will interpret only approved performance evidence and will retain every limitation.</p></div></section>
  return <div className="connected-learning-dashboard">
    <section className="connected-learning-takeaway"><header><span className="connected-ai-orb">✦</span><div>
      <small>Campaign takeaway</small><h2>{report.interpretation.executiveSummary}</h2></div></header>
      <footer><span>{humanizeCode(report.status, true)}</span><span>{humanizeCode(report.interpretation.causalityStatus, true)}</span></footer></section>
    <div className="connected-learning-grid">
      <LearningList title="What the evidence says" icon="chart" items={report.interpretation.findings.map(item => item.summary)} empty="No findings retained." />
      <LearningList title="Limitations" icon="shield" items={report.interpretation.limitations} empty="No limitations retained." />
      <LearningList title="Reusable learnings" icon="brief" items={report.interpretation.learningProposals.map(item => item.text)} empty="No learning proposals retained." />
      <section className="connected-learning-score"><header><Icon name="target" /><h2>Evidence posture</h2></header>
        <strong>{report.interpretation.findings.length}</strong><span>retained finding{report.interpretation.findings.length === 1 ? '' : 's'}</span>
        <p>{report.interpretation.causalityStatus === 'CAUSAL'
          ? 'Causal status is explicitly retained in the approved report.'
          : 'Interpretation remains bounded by the retained causality status and source limitations.'}</p></section>
    </div>
  </div>
}

function ReportingKpi({ label, value, detail, icon }: {
  label: string; value: string; detail: string; icon: 'evidence' | 'chart' | 'shield' | 'brief'
}) {
  return <article><span><Icon name={icon} /></span><div><small>{label}</small><strong>{value}</strong><p>{detail}</p></div></article>
}

function ReportingEmpty({ copy }: { copy: string }) {
  return <p className="connected-reporting-empty">{copy}</p>
}

function LearningList({ title, icon, items, empty }: {
  title: string; icon: 'chart' | 'shield' | 'brief'; items: string[]; empty: string
}) {
  return <section className="connected-learning-list"><header><Icon name={icon} /><h2>{title}</h2></header>
    {items.length ? <ul>{items.slice(0, 6).map((item, index) => <li key={`${item}-${index}`}><span>✓</span>{item}</li>)}</ul>
      : <p>{empty}</p>}</section>
}

function groupMetrics(metrics: Array<{
  metricType: string; value: number; unit: string
}>) {
  const latest = new Map<string, { value: number; unit: string }>()
  metrics.forEach(metric => latest.set(metric.metricType, { value: metric.value, unit: metric.unit }))
  const values = [...latest.entries()]
  const maximum = Math.max(...values.map(([, item]) => Math.abs(item.value)), 1)
  return values.map(([type, item]) => ({
    type,
    relative: Math.min(100, Math.abs(item.value) / maximum * 100),
    display: `${formatMetricValue(item.value)} ${item.unit}`.trim(),
  }))
}

function formatMetricValue(value: number) {
  return new Intl.NumberFormat('en-ZA', { maximumFractionDigits: 2 }).format(value)
}

function LockedMeasurement() {
  return <article className="campaign-section-empty"><Icon name="chart" /><div>
    <h3>Measurement opens after delivery completes</h3>
    <p>Performance evidence can be submitted only after the booked delivery window is closed.</p>
  </div></article>
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
