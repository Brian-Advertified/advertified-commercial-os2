import { useEffect, useState } from 'react'
import { Link, Navigate } from 'react-router-dom'
import { campaignApi } from '../api/campaign-client'
import type { Campaign } from '../api/campaign-schemas'
import { humanMessage } from '../api/client'
import { useWorkspace } from '../auth/workspace-state'
import { campaignViewerRoles } from '../campaign/campaign-roles'
import { Icon } from '../components/Icon'
import { LoadingState, MessageState } from '../components/PageState'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDate, humanizeCode } from '../presentation/format'

export function CampaignsPage() {
  const { selected, loading } = useWorkspace()
  const [campaigns, setCampaigns] = useState<Campaign[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  useEffect(() => {
    if (!selected || !campaignViewerRoles.has(selected.roleCode)) return
    let active = true
    void campaignApi.list(selected.tenantId)
      .then(value => { if (active) setCampaigns(value) })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)) })
    return () => { active = false }
  }, [selected])
  if (loading) return <LoadingState />
  if (!selected) return <Navigate to="/workspaces" replace />
  if (!campaignViewerRoles.has(selected.roleCode)) {
    return <MessageState title="Campaign delivery is not available"
      message="This workspace role cannot view campaign delivery records." />
  }
  if (error) return <MessageState title="Campaigns could not be loaded" message={error} />
  if (!campaigns) return <LoadingState label="Loading campaigns" />
  return <CampaignList campaigns={campaigns} />
}

function CampaignList({ campaigns }: { campaigns: Campaign[] }) {
  return <section className="campaign-list-page" aria-labelledby="campaigns-title">
    <header className="campaign-list-hero"><div><p className="eyebrow">Campaign delivery</p>
      <h1 id="campaigns-title">Move accepted work from funding to measurable delivery.</h1>
      <p>Bookings, creative, proof and reporting stay connected to the exact option selected by the client.</p></div>
      <Link className="secondary-button" to="/funding">Open funding</Link></header>
    <CampaignPortfolioMetrics campaigns={campaigns} />
    {campaigns.length === 0 ? <CampaignEmpty /> : <div className="campaign-card-grid">
      {campaigns.map(campaign => <CampaignCard key={campaign.id} campaign={campaign} />)}
    </div>}
  </section>
}

function CampaignPortfolioMetrics({ campaigns }: { campaigns: Campaign[] }) {
  const confirmed = campaigns.reduce((total, item) => total + item.confirmedBookingCount, 0)
  const required = campaigns.reduce((total, item) => total + item.requiredBookingCount, 0)
  const live = campaigns.filter(item => item.status === masterDataCodes.lifecycleStatuses.live).length
  const proofs = campaigns.reduce((total, item) => total + item.deliveryProofs.length, 0)
  const reports = campaigns.reduce((total, item) => total + item.measurementReports.length, 0)
  return <dl className="campaign-portfolio-metrics" aria-label="Campaign portfolio summary">
    <PortfolioMetric label="Campaigns" value={campaigns.length} detail="funded campaign records" />
    <PortfolioMetric label="Booking lines" value={`${confirmed}/${required}`} detail="confirmed supplier coverage" />
    <PortfolioMetric label="Live now" value={live} detail="campaigns in live delivery" />
    <PortfolioMetric label="Evidence retained" value={proofs} detail={`${reports} measurement reports`} />
  </dl>
}

function PortfolioMetric({ label, value, detail }: {
  label: string
  value: string | number
  detail: string
}) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{detail}</small></div>
}

function CampaignCard({ campaign }: { campaign: Campaign }) {
  const bookingProgress = `${campaign.confirmedBookingCount}/${campaign.requiredBookingCount}`
  return <Link className="campaign-card" to={`/campaigns/${campaign.id}`}>
    <header><span><Icon name="plan" /></span><div><small>Campaign</small>
      <h2>{campaign.title}</h2></div></header>
    <dl><div><dt>Delivery window</dt><dd>{formatDate(campaign.startDate)} – {formatDate(campaign.endDate)}</dd></div>
      <div><dt>Booking coverage</dt><dd>{bookingProgress}</dd></div>
      <div><dt>Funding</dt><dd>{humanizeCode(campaign.fundingStatus, true)}</dd></div></dl>
    <footer><span className={`status-chip ${statusTone(campaign.status)}`}>
      {humanizeCode(campaign.status, true)}</span>
      <small>{campaign.nextActionPermission ? 'Action available' : 'Review current state'}</small>
      <Icon name="arrow" /></footer>
  </Link>
}

function CampaignEmpty() {
  return <article className="campaign-empty"><Icon name="plan" /><div>
    <h2>No funded campaigns yet</h2>
    <p>A campaign appears after an accepted proposal, approved purchase order, issued invoice and confirmed payment.</p>
    <Link className="secondary-button" to="/funding">Open funding</Link></div></article>
}

function statusTone(status: string) {
  const positive: readonly string[] = [
    masterDataCodes.lifecycleStatuses.ready,
    masterDataCodes.lifecycleStatuses.live,
    masterDataCodes.lifecycleStatuses.completed,
  ]
  if (positive.includes(status)) return 'status-positive'
  if (status === masterDataCodes.lifecycleStatuses.creativePending) return 'status-warning'
  return 'status-neutral'
}
