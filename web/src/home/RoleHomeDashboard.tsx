import { type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { Booking } from '../api/booking-schemas'
import type { Campaign } from '../api/campaign-schemas'
import type { InventoryProductPage } from '../api/inventory-schemas'
import type { HumanTask } from '../api/schemas'
import { Icon, type IconName } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney } from '../presentation/format'
import './role-home.css'

type RoleHomeDashboardProps = {
  roleCode: string
  displayName: string
  workspaceName: string
  currency: string
  campaigns: Campaign[]
  bookings: Booking[]
  tasks: HumanTask[]
  inventory: InventoryProductPage | null
}

const supplierRoles = new Set<string>([
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.supplierUser,
  masterDataCodes.roles.influencerRep,
])
const advertiserRoles = new Set<string>([
  masterDataCodes.roles.advertiserAdmin,
  masterDataCodes.roles.advertiserApprover,
])

export function RoleHomeDashboard(props: RoleHomeDashboardProps) {
  if (supplierRoles.has(props.roleCode)) return <SupplierHome {...props} />
  if (advertiserRoles.has(props.roleCode)) return <AdvertiserHome {...props} />
  return null
}

function SupplierHome(props: RoleHomeDashboardProps) {
  const creator = props.roleCode === masterDataCodes.roles.influencerRep
  const inventoryCount = props.inventory?.items.length ?? 0
  const channelCount = new Set(props.inventory?.items.map(item => item.channel) ?? []).size
  return <RoleHome title={`${creator ? 'Creator' : 'Supplier'} workspace`}
    intro={`Welcome, ${firstName(props.displayName)}. This workspace is focused on your supply, requests and delivery obligations.`}>
    <RoleStats stats={[
      ['Inventory visible', String(inventoryCount), 'Current products visible to this workspace'],
      ['Media channels', String(channelCount), 'Channels represented in the visible inventory'],
      ['Bookings', String(props.bookings.length), 'Assigned booking records'],
      ['Tasks', String(props.tasks.length), 'Human actions needing attention'],
    ]} />
    <RoleActions actions={[
      ['/inventory', 'inventory', creator ? 'Manage creator inventory' : 'Manage inventory', 'Review your products, rates, evidence and release status.'],
      ['/marketplace', 'marketplace', 'Marketplace requests', 'Review supply and respond only to requests assigned to your inventory.'],
      ['/bookings', 'reservation', 'Booking confirmations', 'Confirm the exact booked supply assigned to you.'],
      ['/delivery-proof-requests', 'evidence', 'Creative & delivery queue', 'Open proof requests and follow assigned creative or delivery obligations.'],
      ['/tasks', 'tasks', 'Tasks needing attention', 'Work through assigned reviews, evidence and delivery actions.'],
    ]} />
  </RoleHome>
}

function AdvertiserHome(props: RoleHomeDashboardProps) {
  const active = props.campaigns.filter(item => item.status !== masterDataCodes.lifecycleStatuses.completed &&
    item.status !== masterDataCodes.lifecycleStatuses.cancelled)
  const investment = props.bookings.reduce((sum, item) => sum + (item.clientPriceMinor ?? 0), 0)
  return <RoleHome title="Advertiser review workspace"
    intro={`Welcome, ${firstName(props.displayName)}. Review decisions and campaign progress without planner or supplier controls.`}>
    <RoleStats stats={[
      ['Reviews waiting', String(props.tasks.length), 'Assigned approvals or corrections'],
      ['Active campaigns', String(active.length), 'Campaigns currently moving through delivery'],
      ['Booked investment', formatMoney(investment, props.currency, 0), 'Persisted client-price booking lines'],
      ['Bookings', String(props.bookings.length), 'Booking records visible to this advertiser'],
    ]} />
    <RoleActions actions={[
      ['/opportunities', 'target', 'Opportunities & strategy', 'Review assigned commercial opportunities and approved strategy evidence.'],
      ['/briefs', 'brief', 'Briefs & proposals', 'Review assigned Briefs and client proposal decisions.'],
      ['/campaigns', 'plan', 'Campaign progress', 'Follow booking, creative, proof and delivery readiness.'],
      ['/measurement', 'chart', 'Measurement & reporting', 'Review approved evidence and campaign measurement.'],
      ['/funding', 'money', 'Finance records', 'View funding and payment records available to your role.'],
    ]} />
  </RoleHome>
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
