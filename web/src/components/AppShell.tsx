import { useEffect, useLayoutEffect, useRef, useState, type FormEvent } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { api, humanMessage } from '../api/client'
import type { CurrentUser, Workspace } from '../api/schemas'
import { opportunityApi } from '../api/opportunity-client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { CampaignFlowProvider } from '../campaign-flow/CampaignFlowProvider'
import { useCampaignFlowResolution } from '../campaign-flow/useCampaignFlow'
import { masterDataCodes } from '../generated/master-data-codes'
import { notifications } from '../notifications/notifications'
import { humanizeCode } from '../presentation/format'
import { ApprovedFlowRail } from './ApprovedFlowRail'
import { Icon, type IconName } from './Icon'

type Destination = {
  to: string
  label: string
  icon: IconName
  roles?: ReadonlySet<string>
}

type ShellData = {
  tenantId: string
  user: CurrentUser | null
  taskCount: number
}

const adminRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

const platformAdminRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
])

const agencyPlanningRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
  masterDataCodes.roles.agencyCampaignUser,
])

const advertiserRoles = new Set<string>([
  masterDataCodes.roles.advertiserAdmin,
  masterDataCodes.roles.advertiserApprover,
])

const planningViewerRoles = new Set<string>([
  ...agencyPlanningRoles,
  ...advertiserRoles,
])

const supplierOperatorRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.inventoryOps,
  masterDataCodes.roles.supplierUser,
  masterDataCodes.roles.influencerRep,
])

const inventoryWorkspaceRoles = new Set<string>([
  ...agencyPlanningRoles,
  ...supplierOperatorRoles,
])

const buyerMarketplaceRoles = new Set<string>([
  ...agencyPlanningRoles,
  ...supplierOperatorRoles,
])

const briefCreatorRoles = new Set<string>([
  ...agencyPlanningRoles,
])

const agencyPartnerRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

const influencerDirectoryRoles = new Set<string>([
  ...agencyPlanningRoles,
  masterDataCodes.roles.influencerRep,
])

const destinations: readonly Destination[] = [
  { to: '/home', label: 'Home', icon: 'home' },
  { to: '/opportunities', label: 'Opportunities', icon: 'target', roles: agencyPlanningRoles },
  { to: '/marketplace', label: 'Marketplace', icon: 'marketplace', roles: buyerMarketplaceRoles },
  { to: '/strategy-stp', label: 'Audience Intelligence', icon: 'users', roles: planningViewerRoles },
  { to: '/proposals', label: 'Proposals', icon: 'proposal', roles: planningViewerRoles },
  { to: '/campaigns', label: 'Campaigns', icon: 'plan', roles: planningViewerRoles },
  { to: '/measurement', label: 'Reports & Insights', icon: 'chart', roles: planningViewerRoles },
]

const utilityDestinations: readonly Destination[] = [
  { to: '/advertisers', label: 'Advertisers', icon: 'commercial', roles: agencyPlanningRoles },
  { to: '/suppliers', label: 'Suppliers', icon: 'reservation', roles: inventoryWorkspaceRoles },
  { to: '/agency-partners', label: 'Agency Partners', icon: 'users', roles: agencyPartnerRoles },
  { to: '/influencers', label: 'Influencers & Creators', icon: 'profile', roles: influencerDirectoryRoles },
  { to: '/tasks', label: 'Tasks & Approvals', icon: 'tasks' },
  { to: '/admin/onboarding', label: 'Partners & Access', icon: 'users', roles: platformAdminRoles },
  { to: '/admin/commercial', label: 'Settings', icon: 'commercial', roles: adminRoles },
]

const exactNavigation: Readonly<Record<string, readonly string[]>> = {
  Home: ['/home'],
  'Tasks & Approvals': ['/tasks', '/approvals'],
}

const prefixNavigation: Readonly<Record<string, readonly string[]>> = {
  Opportunities: ['/opportunities'],
  Marketplace: ['/marketplace'],
  'Audience Intelligence': ['/strategy-stp', '/stp/'],
  Proposals: ['/proposals', '/briefs/'],
  Campaigns: ['/campaigns', '/funding', '/bookings', '/creative-assets/', '/delivery-proofs/'],
  'Reports & Insights': ['/measurement', '/reports', '/performance-evidence/', '/measurement-reports/'],
  Advertisers: ['/advertisers'],
  Suppliers: ['/suppliers', '/inventory'],
  'Agency Partners': ['/agency-partners'],
  'Influencers & Creators': ['/influencers'],
  'Partners & Access': ['/admin/onboarding'],
  Settings: ['/admin/commercial', '/admin/agents'],
}

function navigationActive(item: Destination, pathname: string) {
  const exact = exactNavigation[item.label]?.includes(pathname) ?? false
  const prefix = prefixNavigation[item.label]?.some(value => pathname.startsWith(value)) ?? false
  return exact || prefix
}

function Navigation({ roleCode, pathname, taskCount }: {
  roleCode?: string
  pathname: string
  taskCount: number
}) {
  const renderDestination = (item: Destination) => <NavLink key={item.to} to={item.to}
    className={() => `approved-nav-link${navigationActive(item, pathname) ? ' is-active' : ''}`}>
    <Icon name={item.icon} /><span>{item.label}</span>
    {item.label === 'Tasks & Approvals' && taskCount > 0 && <em>{taskCount}</em>}
  </NavLink>
  return <nav className="approved-navigation" aria-label="Workspace navigation">
    {destinations.filter(item => !item.roles || item.roles.has(roleCode ?? '')).map(renderDestination)}
    <div className="approved-nav-divider" aria-hidden="true" />
    {utilityDestinations.filter(item => !item.roles || item.roles.has(roleCode ?? '')).map(renderDestination)}
  </nav>
}

function Wordmark() {
  return <NavLink className="approved-wordmark" to="/home" aria-label="Advertified home">
    <img src="/advertified-wordmark.png" alt="Advertified" />
  </NavLink>
}

function GlobalSearch() {
  const navigate = useNavigate()
  const location = useLocation()
  const inputRef = useRef<HTMLInputElement>(null)
  const query = location.pathname === '/search'
    ? new URLSearchParams(location.search).get('q') ?? ''
    : ''

  useLayoutEffect(() => {
    const focusSearch = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || !event.shiftKey || event.key.toLowerCase() !== 'k') return
      event.preventDefault()
      inputRef.current?.focus()
      inputRef.current?.select()
    }
    window.addEventListener('keydown', focusSearch, true)
    return () => window.removeEventListener('keydown', focusSearch, true)
  }, [])

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const value = new FormData(event.currentTarget).get('q')
    const normalized = typeof value === 'string' ? value.trim() : ''
    if (!normalized) { inputRef.current?.focus(); return }
    navigate(`/search?q=${encodeURIComponent(normalized)}`)
  }

  return <form className="approved-global-search" role="search" onSubmit={submit}>
    <button className="approved-global-search-submit" type="submit" aria-label="Submit search">
      <Icon name="search" />
    </button>
    <input ref={inputRef} name="q" type="search" defaultValue={query}
      aria-label="Search Advertified"
      aria-keyshortcuts="Control+Shift+K Meta+Shift+K"
      placeholder="Search opportunities, inventory, audiences…" />
    <kbd aria-hidden="true">⇧⌘ K</kbd>
  </form>
}

function TopbarPrimaryActions({ notificationCount }: {
  workspace: Workspace | null
  notificationCount: number
}) {
  return <NavLink className="approved-icon-button" to="/notifications" aria-label="Notifications">
    <Icon name="bell" />{notificationCount > 0 && <i>{notificationCount}</i>}
  </NavLink>
}

function GlobalTopbar({ workspace, user, notificationCount, onSignOut }: {
  workspace: Workspace | null
  user: CurrentUser | null
  notificationCount: number
  onSignOut: () => void
}) {
  const displayName = user?.displayName ?? 'My account'
  const initial = displayName.trim().charAt(0).toUpperCase() || 'A'
  return <header className="approved-home-topbar">
    <GlobalSearch />
    <div className="approved-home-actions">
      <NavLink className="approved-icon-button" to="/faq" aria-label="Help"
        title="Open Advertified help">?</NavLink>
      <TopbarPrimaryActions workspace={workspace} notificationCount={notificationCount} />
      <NavLink className="approved-user-chip" to="/profile" aria-label={`${displayName} profile`}><span>{initial}</span>
        <div><strong>{displayName}</strong><small>{workspace ? humanizeCode(workspace.roleCode, true) : 'Advertified'}</small></div><b>⌄</b></NavLink>
      <button className="approved-signout" type="button" onClick={onSignOut}>Sign out</button>
    </div>
  </header>
}

export function AppShell() {
  const { selected } = useWorkspace()
  const location = useLocation()
  const mainContentRef = useRouteFocus(location.pathname)
  const shellData = useShellData(selected?.tenantId)
  const endSession = useEndSession()
  const user = shellData?.user ?? null
  const taskCount = shellData?.taskCount ?? 0
  const routeKey = `${location.pathname}${location.search}`
  return <CampaignFlowProvider routeKey={routeKey}><div
    className="app-shell approved-shell approved-shell--workspace">
    <a className="skip-link" href="#main-content">Skip to main content</a>
    <ShellSidebar workspace={selected} pathname={location.pathname} taskCount={taskCount} />
    <div className="approved-application-column">
      <GlobalTopbar workspace={selected} user={user} notificationCount={taskCount}
        onSignOut={endSession} />
      <CampaignFlowRail pathname={location.pathname} hash={location.hash} />
      <main ref={mainContentRef} className="page-frame approved-page-frame" id="main-content" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  </div></CampaignFlowProvider>
}

function ShellSidebar({ workspace, pathname, taskCount }: {
  workspace?: Workspace | null
  pathname: string
  taskCount: number
}) {
  const canCreateBrief = Boolean(workspace?.roleCode && briefCreatorRoles.has(workspace.roleCode))
  return <aside className="approved-sidebar"><Wordmark />
    {canCreateBrief && <NavLink className="approved-new-campaign-nav" to="/briefs/new">
      <span className="new-campaign-plus"><Icon name="plus" /></span><span>New Campaign</span>
    </NavLink>}
    <Navigation roleCode={workspace?.roleCode} pathname={pathname} taskCount={taskCount} />
    <div className="approved-sidebar-spacer" />
    <div className="approved-sidebar-brand-panel" aria-hidden="true">
      <img src="/assets/media-inventory/out-of-home-real.jpg" alt="" />
      <strong>Real people.<br />Real places.<br />Real impact.</strong><i />
    </div>
  </aside>
}

function useRouteFocus(pathname: string) {
  const mainContentRef = useRef<HTMLElement>(null)
  const previousPath = useRef(pathname)
  useLayoutEffect(() => {
    if (previousPath.current !== pathname) mainContentRef.current?.focus()
    previousPath.current = pathname
  }, [pathname])
  return mainContentRef
}

function useShellData(tenantId?: string) {
  const [shellData, setShellData] = useState<ShellData | null>(null)
  useEffect(() => {
    if (!tenantId) return
    let active = true
    void Promise.all([api.getCurrentUser(), opportunityApi.listTasks(tenantId)])
      .then(([profile, tasks]) => {
        if (active) setShellData({ tenantId, user: profile.user, taskCount: tasks.length })
      })
      .catch(() => { if (active) setShellData({ tenantId, user: null, taskCount: 0 }) })
    return () => { active = false }
  }, [tenantId])
  return shellData?.tenantId === tenantId ? shellData : null
}

function useEndSession() {
  const { signOut } = useSession()
  const navigate = useNavigate()
  return async () => {
    try {
      const redirected = await signOut()
      if (!redirected) navigate('/sign-in', { replace: true })
    } catch (failure) {
      notifications.failure(humanMessage(failure))
    }
  }
}

function CampaignFlowRail({ pathname, hash }: { pathname: string; hash: string }) {
  const campaignFlow = useCampaignFlowResolution()
  return <ApprovedFlowRail pathname={pathname} hash={hash} campaignFlow={campaignFlow} />
}
