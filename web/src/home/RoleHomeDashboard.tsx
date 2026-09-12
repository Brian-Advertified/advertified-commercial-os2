import { type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { Booking } from '../api/booking-schemas'
import type { Campaign } from '../api/campaign-schemas'
import type { InventoryProductPage } from '../api/inventory-schemas'
import type { MarketplaceRfq } from '../api/marketplace-schemas'
import type { PlanningSummary } from '../api/planning-schemas'
import type { ProposalSummary } from '../api/proposal-schemas'
import type { HumanTask } from '../api/schemas'
import { Icon, type IconName } from '../components/Icon'
import { homeCopy, investmentDescription } from '../content/home-copy'
import { homeAudience } from './dashboard-data'
import { bookingInvestment } from './dashboard-metrics'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatDate, formatMoney, humanizeCode } from '../presentation/format'
import './role-home.css'

type RoleHomeDashboardProps = {
  roleCode: string
  displayName: string
  workspaceName: string
  currency: string
  campaigns: Campaign[]
  bookings: Booking[]
  tasks: HumanTask[]
  planning: PlanningSummary[]
  proposals: ProposalSummary[]
  rfqs: MarketplaceRfq[]
  inventory: InventoryProductPage | null
}

export function RoleHomeDashboard(props: RoleHomeDashboardProps) {
  const audience = homeAudience(props.roleCode)
  if (audience === 'supplier' || audience === 'creator') return <SupplierHome {...props} />
  if (audience === 'advertiser') return <AdvertiserHome {...props} />
  return null
}

function SupplierHome(props: RoleHomeDashboardProps) {
  const creator = props.roleCode === masterDataCodes.roles.influencerRep
  const inventoryCount = props.inventory?.items.length ?? 0
  const inbound = props.rfqs.filter(item => item.status === masterDataCodes.marketplaceRfqStatuses.sent)
  const responded = props.rfqs.filter(item => item.status === masterDataCodes.marketplaceRfqStatuses.responded)
  const accepted = props.rfqs.filter(item => item.status === masterDataCodes.marketplaceRfqStatuses.accepted)
  return <RoleHome title={`${creator ? 'Creator' : 'Supplier'} commercial workspace`}
    intro={`Welcome, ${firstName(props.displayName)}. See buyer demand, respond to qualified requests and carry accepted work through delivery.`}>
    <RoleStats stats={[
      ['Buyer requests', String(inbound.length), 'Qualified requests waiting for your response'],
      ['Quotes in market', String(responded.length), 'Supplier responses awaiting buyer decision'],
      ['Accepted quotes', String(accepted.length), 'Marketplace responses accepted by buyers'],
      ['Bookings', String(props.bookings.length), 'Commercial commitments assigned to your supply'],
    ]} />
    <SupplierDemandPanel rfqs={props.rfqs} inventoryCount={inventoryCount} />
    <RoleActions actions={[
      ['/marketplace', 'marketplace', 'Buyer demand', `${inbound.length} request${inbound.length === 1 ? '' : 's'} currently need a supplier response.`],
      ['/inventory', 'inventory', creator ? 'Manage creator inventory' : 'Manage inventory', `${inventoryCount} product${inventoryCount === 1 ? '' : 's'} are currently visible in this workspace.`],
      ['/bookings', 'reservation', 'Booking commitments', 'Confirm the exact media, dates and commercial terms the buyer selected.'],
      ['/delivery-proof-requests', 'evidence', 'Creative & delivery queue', 'Complete booked production requirements and return delivery evidence.'],
      ['/tasks', 'tasks', 'Tasks needing attention', `${props.tasks.length} assigned action${props.tasks.length === 1 ? '' : 's'} currently need attention.`],
    ]} />
  </RoleHome>
}

function SupplierDemandPanel({ rfqs, inventoryCount }: {
  rfqs: MarketplaceRfq[]
  inventoryCount: number
}) {
  const active = rfqs.filter(item => item.status !== masterDataCodes.marketplaceRfqStatuses.expired &&
      item.status !== masterDataCodes.marketplaceRfqStatuses.draft)
    .sort((left, right) => left.dueAtUtc.localeCompare(right.dueAtUtc)).slice(0, 5)
  return <section className="role-commercial-panel" aria-labelledby="supplier-demand-title">
    <header><div><p className="eyebrow">Qualified demand</p><h2 id="supplier-demand-title">Buyers asking for your media</h2>
      <p>{inventoryCount > 0
        ? 'Marketplace demand is shown against real published supply, not generic sales leads.'
        : 'Publish governed inventory so buyer requests can be matched to real supply.'}</p></div>
      <Link to="/marketplace">Open marketplace →</Link></header>
    {active.length === 0 ? <p className="role-commercial-empty">No active buyer request is assigned to this workspace right now.</p> :
      <div className="role-demand-list">{active.map(item => <Link to="/marketplace" key={item.id}>
        <div><strong>{item.subject}</strong><span>{item.productName} · {formatDate(item.requestedStart)} – {formatDate(item.requestedEnd)}</span></div>
        <div><b>{humanizeCode(item.status, true)}</b><small>Due {formatDate(item.dueAtUtc)}</small></div>
      </Link>)}</div>}
  </section>
}

function AdvertiserHome(props: RoleHomeDashboardProps) {
  const active = props.campaigns.filter(item => item.status !== masterDataCodes.lifecycleStatuses.completed &&
    item.status !== masterDataCodes.lifecycleStatuses.cancelled)
  const investment = bookingInvestment(props.bookings, props.currency)
  const waiting = props.proposals.filter(item => item.status === masterDataCodes.lifecycleStatuses.sent)
  const approvedPlans = props.planning.filter(item =>
    item.mediaPlanStatus === masterDataCodes.lifecycleStatuses.approved).length
  return <RoleHome title="Advertiser decision workspace"
    intro={`Welcome, ${firstName(props.displayName)}. See what needs your decision, what has been committed and what the campaign is delivering.`}>
    <RoleStats stats={[
      ['Decisions waiting', String(waiting.length), 'Client proposals currently awaiting a decision'],
      ['Approved media plans', String(approvedPlans), 'Evidence-backed plans ready or already used in proposals'],
      ['Active campaigns', String(active.length), 'Campaigns currently moving through execution'],
      [homeCopy.confirmedMedia, investment.totalMinor === null ? '—' : formatMoney(investment.totalMinor, props.currency, 0),
        investment.totalMinor === null ? homeCopy.unknownAmount : investmentDescription(props.currency, investment.otherCurrencies)],
    ]} />
    <AdvertiserDecisionPanel proposals={props.proposals} />
    <RoleActions actions={[
      ['/briefs', 'brief', 'Briefs & proposals', 'Review what Advertified understood and compare client proposal choices.'],
      ['/campaigns', 'plan', 'Campaign progress', 'Follow bookings, creative readiness, delivery proof and campaign state.'],
      ['/measurement', 'chart', 'Measurement & learning', 'Review evidence-backed outcomes, limitations and retained campaign learning.'],
      ['/tasks', 'tasks', 'Decisions & tasks', `${props.tasks.length} assigned review${props.tasks.length === 1 ? '' : 's'} currently need attention.`],
    ]} />
  </RoleHome>
}

function AdvertiserDecisionPanel({ proposals }: { proposals: ProposalSummary[] }) {
  const visible = [...proposals].sort((left, right) =>
    right.createdAtUtc.localeCompare(left.createdAtUtc)).slice(0, 5)
  return <section className="role-commercial-panel" aria-labelledby="advertiser-decisions-title">
    <header><div><p className="eyebrow">Client decisions</p><h2 id="advertiser-decisions-title">Campaign choices ready for review</h2>
      <p>Proposal status is tied to retained planning records and the exact option ultimately selected.</p></div>
      <Link to="/briefs">Open campaign work →</Link></header>
    {visible.length === 0 ? <p className="role-commercial-empty">No client proposal is available for this advertiser yet.</p> :
      <div className="role-demand-list">{visible.map(item => <Link to={`/proposals/${item.id}`} key={item.id}>
        <div><strong>{item.title}</strong><span>Proposal version {item.versionNumber}</span></div>
        <div><b>{item.status === masterDataCodes.lifecycleStatuses.sent ? 'Decision needed' : humanizeCode(item.status, true)}</b>
          <small>{formatDate(item.createdAtUtc)}</small></div>
      </Link>)}</div>}
  </section>
}

function RoleHome({ title, intro, children }: {
  title: string; intro: string; children: ReactNode
}) {
  return <section className="approved-dashboard role-home" aria-labelledby="role-home-title">
    <header className="approved-dashboard-greeting">
      <p className="eyebrow">{title}</p><h1 id="role-home-title">{title}</h1><p>{intro}</p>
    </header>
    {children}
  </section>
}

function RoleStats({ stats }: { stats: Array<[string, string, string]> }) {
  return <div className="approved-kpi-grid">{stats.map(([label, value, detail]) =>
    <article className="approved-kpi-card" key={label}><div><span>{label}</span>
      <strong>{value}</strong><small>{detail}</small></div></article>)}</div>
}

type RoleAction = [string, IconName, string, string]
function RoleActions({ actions }: { actions: RoleAction[] }) {
  return <section className="role-home-actions" aria-label="Workspace priorities">
    {actions.map(([to, icon, title, detail]) => <Link to={to} key={to}>
      <span><Icon name={icon} /></span><div><strong>{title}</strong><small>{detail}</small></div>
      <b aria-hidden="true">→</b></Link>)}
  </section>
}

function firstName(name: string) { return name.trim().split(/\s+/)[0] || 'there' }
