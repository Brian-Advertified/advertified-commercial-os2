import type { Campaign } from '../api/campaign-schemas'
import { formatDate, humanizeCode } from '../presentation/format'

export function CampaignHeader({ campaign }: { campaign: Campaign }) {
  const bookingCoverage = `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount}`
  return <>
    <header className="campaign-workspace-hero">
      <div className="campaign-workspace-copy"><p className="eyebrow">Campaign delivery workspace</p>
        <h1 id="campaign-title">{campaign.title}</h1>
        <p>Carry the exact client-selected work through booking, creative, live delivery, proof and measurement without losing its approved commercial lineage.</p></div>
      <div className="campaign-header-state">
        <span className="status-chip status-neutral">{humanizeCode(campaign.status, true)}</span>
        <Context label="Delivery window"
          value={`${formatDate(campaign.startDate)} – ${formatDate(campaign.endDate)}`} />
        <Context label="Funding" value={humanizeCode(campaign.fundingStatus, true)} />
      </div>
    </header>
    <dl className="campaign-metric-strip" aria-label="Campaign delivery summary">
      <Metric label="Booking coverage" value={bookingCoverage} detail="confirmed supplier lines" />
      <Metric label="Creative requirements" value={campaign.creative?.requirements.length ?? 0}
        detail={campaign.creative?.readyForApproval ? 'ready for approval' : 'current requirements'} />
      <Metric label="Proof records" value={campaign.deliveryProofs.length}
        detail="retained delivery evidence" />
      <Metric label="Measurement reports" value={campaign.measurementReports.length}
        detail="retained report versions" />
    </dl>
  </>
}

function Context({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>
}

function Metric({ label, value, detail }: {
  label: string
  value: string | number
  detail: string
}) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{detail}</small></div>
}
