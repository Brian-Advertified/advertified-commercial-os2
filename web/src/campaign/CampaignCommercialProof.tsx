import type { Campaign, MeasurementReport } from '../api/campaign-schemas'
import { CommercialValueProof, type CommercialProofMetric } from '../components/CommercialValueProof'
import type { CampaignWorkspaceModel } from './useCampaignWorkspace'

export function CampaignCommercialProof({ model }: { model: CampaignWorkspaceModel }) {
  const learning = latestReviewedLearning(model.campaign)
  return <>
    <CommercialValueProof title="The commercial decision stays connected through delivery"
      description="Advertified keeps the selected proposal, supplier commitments, creative readiness, proof and measurement on one accountable campaign journey."
      metrics={campaignProofMetrics(model)}
      note={`Current campaign state: ${model.campaign.status}. No delivery or performance is inferred from workflow progress alone.`} />
    {learning && <CampaignLearningPanel report={learning} />}
  </>
}

function campaignProofMetrics(model: CampaignWorkspaceModel): CommercialProofMetric[] {
  return [
    bookingMetric(model),
    creativeMetric(model.campaign),
    deliveryMetric(model.campaign),
    measurementMetric(model.campaign),
  ]
}

function latestReviewedLearning(campaign: Campaign) {
  return [...campaign.measurementReports]
    .filter(item => item.reviewedAtUtc !== null)
    .sort((left, right) => (left.reviewedAtUtc ?? '').localeCompare(right.reviewedAtUtc ?? ''))
    .at(-1) ?? null
}

function CampaignLearningPanel({ report }: { report: MeasurementReport }) {
  return <section className="campaign-learning-panel" aria-labelledby="campaign-learning-title">
    <header><div><p className="eyebrow">Campaign learning</p>
      <h2 id="campaign-learning-title">What this campaign taught Advertified</h2>
      <p>{report.interpretation.executiveSummary}</p></div>
      <span>{report.interpretation.causalityStatus.replaceAll('_', ' ')}</span></header>
    <div className="campaign-learning-grid">
      <article><h3>Evidence-backed findings</h3>
        {report.interpretation.findings.length > 0 ? <ul>{report.interpretation.findings.slice(0, 4).map(item =>
          <li key={item.title}><strong>{item.title}</strong><span>{item.summary}</span></li>)}</ul> :
          <p>No reviewed findings are retained.</p>}</article>
      <article><h3>What should change next time</h3>
        {report.interpretation.learningProposals.length > 0 ? <ul>{report.interpretation.learningProposals.slice(0, 4).map((item, index) =>
          <li key={`${item.text}-${index}`}><strong>{item.requiresNewApproval ? 'New approval required' : 'Learning retained'}</strong><span>{item.text}</span></li>)}</ul> :
          <p>No learning proposal is retained yet.</p>}</article>
    </div>
    {report.interpretation.limitations.length > 0 && <footer><strong>Measurement limits:</strong> {report.interpretation.limitations.slice(0, 3).join(' · ')}</footer>}
  </section>
}

function bookingMetric(model: CampaignWorkspaceModel): CommercialProofMetric {
  const { campaign, bookings } = model
  const complete = campaign.requiredBookingCount > 0 &&
    campaign.confirmedBookingCount >= campaign.requiredBookingCount
  return {
    label: 'Supplier commitments', value: `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount}`,
    detail: 'Booking progress remains tied to the exact client-selected plan.', icon: 'inventory',
    tone: complete ? 'positive' : 'warning',
    why: `The campaign records ${bookingCountLabel(campaign.requiredBookingCount)} and ${campaign.confirmedBookingCount} confirmed. ${bookingCountLabel(bookings.length)} are loaded for this plan.`,
  }
}

function creativeMetric(campaign: Campaign): CommercialProofMetric {
  if (campaign.creativeApprovedAtUtc) return {
    label: 'Creative readiness', value: 'Approved', detail: 'Creative approval is retained before execution.',
    icon: 'proposal', tone: 'positive', why: 'A persisted creative approval timestamp exists on the campaign.',
  }
  if (campaign.creativeRequestedAtUtc) return {
    label: 'Creative readiness', value: 'In progress',
    detail: 'Creative status remains visible instead of being detached from the booking.',
    icon: 'proposal', tone: 'blue', why: 'A creative request exists but no approval timestamp exists yet.',
  }
  return {
    label: 'Creative readiness', value: 'Not started',
    detail: 'Creative status remains visible instead of being detached from the booking.',
    icon: 'proposal', tone: 'neutral', why: 'No creative request or approval timestamp exists on the current campaign.',
  }
}

function deliveryMetric(campaign: Campaign): CommercialProofMetric {
  const reviewed = campaign.deliveryProofs.filter(item => item.reviewedAtUtc !== null).length
  const total = campaign.deliveryProofs.length
  return {
    label: 'Delivery evidence', value: `${reviewed}/${total} reviewed`,
    detail: total > 0 ? 'Proof remains linked to the campaign and booking evidence.' : 'No delivery proof has been retained yet.',
    icon: 'evidence', tone: reviewed > 0 ? 'positive' : 'neutral',
    why: 'Reviewed proof counts only delivery-proof records with a persisted review timestamp.',
  }
}

function measurementMetric(campaign: Campaign): CommercialProofMetric {
  const reviewed = campaign.measurementReports.filter(item => item.reviewedAtUtc !== null).length
  const total = campaign.measurementReports.length
  return {
    label: 'Measurement learning', value: `${reviewed}/${total} reviewed`,
    detail: reviewed > 0 ? 'Reviewed measurement can now inform the campaign learning record.' : 'Measurement is not claimed before evidence and review exist.',
    icon: 'chart', tone: reviewed > 0 ? 'positive' : 'neutral',
    why: 'Reviewed measurement counts only retained measurement reports with a persisted review timestamp.',
  }
}

function bookingCountLabel(count: number) {
  return `${count} booking record${count === 1 ? '' : 's'}`
}
