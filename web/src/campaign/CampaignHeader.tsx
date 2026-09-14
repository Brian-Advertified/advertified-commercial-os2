import type { Campaign } from '../api/campaign-schemas'
import { formatDate, humanizeCode } from '../presentation/format'

export function CampaignHeader({ campaign }: { campaign: Campaign }) {
  const bookingCoverage = `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount}`
  return <section className="connected-campaign-summary" aria-label="Campaign summary">
    <div className="connected-campaign-thumb" aria-hidden="true" />
    <div className="connected-campaign-summary-copy"><header><h2 id="campaign-title">{campaign.title}</h2>
      <span className="status-chip status-neutral">{humanizeCode(campaign.status, true)}</span></header>
      <p>Campaign delivery stays bound to the exact selected proposal, media plan, bookings and retained evidence.</p>
      <dl><div><dt>Flight</dt><dd>{formatDate(campaign.startDate)} – {formatDate(campaign.endDate)}</dd></div>
        <div><dt>Funding</dt><dd>{humanizeCode(campaign.fundingStatus, true)}</dd></div>
        <div><dt>Booking coverage</dt><dd>{bookingCoverage}</dd></div>
        <div><dt>Measurement reports</dt><dd>{campaign.measurementReports.length}</dd></div></dl>
    </div>
  </section>
}
