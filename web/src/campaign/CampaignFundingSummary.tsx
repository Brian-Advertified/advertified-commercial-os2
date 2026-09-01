import { Link } from 'react-router-dom'
import type { Campaign } from '../api/campaign-schemas'
import { Icon } from '../components/Icon'
import { humanizeCode } from '../presentation/format'

export function CampaignFundingSummary({ campaign }: { campaign: Campaign }) {
  return <section id="funding-stage" className="campaign-workspace-section campaign-funding-summary">
    <header className="campaign-section-heading"><div><p className="eyebrow">Commercial foundation</p>
      <h2>Funding</h2><p>This campaign was created only after the exact selected option, purchase order, invoice and payment evidence reconciled.</p></div>
      <span className="status-chip status-positive">{humanizeCode(campaign.fundingStatus, true)}</span></header>
    <div className="campaign-funding-card"><Icon name="money" /><div>
      <strong>Funding confirmed for the selected proposal version</strong>
      <p>Any changed amount, option or commercial input requires a new approved funding path.</p></div>
      <Link className="secondary-button" to="/funding">Open funding history</Link></div>
  </section>
}
