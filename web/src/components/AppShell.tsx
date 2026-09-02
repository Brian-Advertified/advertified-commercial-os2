import { useEffect, useRef, useState, type FormEvent } from 'react'
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

const oohInboxRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
])

const destinations: readonly Destination[] = [
  { to: '/home', label: 'Home', icon: 'home' },
  { to: '/opportunities', label: 'Opportunities', icon: 'target' },
  { to: '/briefs', label: 'Briefs', icon: 'brief' },
  { to: '/inventory', label: 'Inventory', icon: 'inventory' },
  { to: '/marketplace', label: 'Marketplace', icon: 'marketplace' },
  { to: '/ooh-inbox', label: 'OOH Inbox', icon: 'inbox', roles: oohInboxRoles },
  { to: '/bookings', label: 'Bookings', icon: 'reservation' },
  { to: '/campaigns', label: 'Campaigns', icon: 'plan' },
  { to: '/tasks', label: 'Tasks', icon: 'tasks' },
  { to: '/funding', label: 'Finance', icon: 'money' },
  { to: '/admin/commercial', label: 'Settings', icon: 'commercial', roles: adminRoles },
]

const exactNavigation: Readonly<Record<string, readonly string[]>> = {
  Home: ['/home'],
  'OOH Inbox': ['/ooh-inbox'],
  Tasks: ['/tasks', '/approvals'],
}

const prefixNavigation: Readonly<Record<string, readonly string[]>> = {
  Opportunities: ['/opportunities'],
  Briefs: ['/briefs', '/stp/', '/planning/', '/proposals/'],
  Inventory: ['/inventory'],
  Marketplace: ['/marketplace'],
  Bookings: ['/bookings'],
  Campaigns: [
    '/campaigns', '/creative-assets/', '/delivery-proofs/',
    '/performance-evidence/', '/measurement-reports/',
  ],
  Finance: ['/funding'],
  Settings: ['/admin/commercial'],
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
  return <nav className="approved-navigation" aria-label="Workspace navigation">
    {destinations.filter(item => !item.roles || item.roles.has(roleCode ?? '')).map(item =>
      <NavLink key={item.to} to={item.to}
        className={() => `approved-nav-link${navigationActive(item, pathname) ? ' is-active' : ''}`}>
        <Icon name={item.icon} /><span>{item.label}</span>
        {item.label === 'Tasks' && taskCount > 0 && <em>{taskCount}</em>}
      </NavLink>)}
  </nav>
}

function Wordmark() {
  return <NavLink className="approved-wordmark" to="/home" aria-label="Advertified home">
    <span className="approved-wordmark-mark">A</span><strong>ADVERTIFIED</strong>
  </NavLink>
}

function WorkspaceCard({ workspace }: { workspace: Workspace | null }) {
  const initial = workspace?.name.trim().charAt(0).toUpperCase() || 'A'
  return <NavLink className="approved-workspace-card" to="/workspaces">
    <span>{initial}</span><div><strong>{workspace?.name ?? 'Choose workspace'}</strong>
      <small>{workspace ? humanizeCode(workspace.roleCode, true) : 'Workspace'}</small></div>
    <b>⌄</b>
  </NavLink>
}

function GlobalSearch() {
  const navigate = useNavigate()
  const location = useLocation()
  const inputRef = useRef<HTMLInputElement>(null)
  const query = location.pathname === '/search'
    ? new URLSearchParams(location.search).get('q') ?? ''
    : ''

  useEffect(() => {
    const focusSearch = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'k') return
      event.preventDefault()
      inputRef.current?.focus()
      inputRef.current?.select()
    }
    window.addEventListener('keydown', focusSearch)
    return () => window.removeEventListener('keydown', focusSearch)
  }, [])

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalized = inputRef.current?.value.trim() ?? ''
    if (!normalized) { inputRef.current?.focus(); return }
    navigate(`/search?q=${encodeURIComponent(normalized)}`)
  }

  return <form className="approved-global-search" role="search" onSubmit={submit}>
    <button className="approved-global-search-submit" type="submit" aria-label="Submit search">
      <Icon name="search" />
    </button>
    <input key={query} ref={inputRef} type="search" defaultValue={query}
      aria-label="Search Advertified"
      aria-keyshortcuts="Control+K Meta+K"
      placeholder="Search campaigns, briefs, inventory, reports…" />
    <kbd aria-hidden="true">⌘ K</kbd>
  </form>
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
      <NavLink className="approved-new-button" to="/briefs/new"><Icon name="plus" /> New <span>⌄</span></NavLink>
      <NavLink className="approved-icon-button" to="/tasks" aria-label="Notifications"><Icon name="bell" />{notificationCount > 0 && <i>{notificationCount}</i>}</NavLink>
      {workspace && oohInboxRoles.has(workspace.roleCode) && <NavLink
        className="approved-icon-button" to="/ooh-inbox" aria-label="Messages"
        title="Open OOH proposal inbox"><Icon name="inbox" /></NavLink>}
      <NavLink className="approved-icon-button" to="/faq" aria-label="Help"
        title="Open Advertified help">?</NavLink>
      <NavLink className="approved-user-chip" to="/profile" aria-label={`${displayName} profile`}><span>{initial}</span>
        <div><strong>{displayName}</strong><small>{workspace?.name ?? 'Advertified'}</small></div><b>⌄</b></NavLink>
      <button className="approved-signout" type="button" onClick={onSignOut}>Sign out</button>
    </div>
  </header>
}

export function AppShell() {
  const { signOut } = useSession()
  const { selected } = useWorkspace()
  const navigate = useNavigate()
  const location = useLocation()
  const mainContentRef = useRef<HTMLElement>(null)
  const previousPath = useRef(location.pathname)
  const [shellData, setShellData] = useState<ShellData | null>(null)
  const tenantId = selected?.tenantId
  const activeShellData = shellData?.tenantId === tenantId ? shellData : null
  const user = activeShellData?.user ?? null
  const taskCount = activeShellData?.taskCount ?? 0

  useEffect(() => {
    if (previousPath.current !== location.pathname) mainContentRef.current?.focus()
    previousPath.current = location.pathname
  }, [location.pathname])

  useEffect(() => {
    if (!tenantId) return
    let active = true
    void Promise.all([api.getCurrentUser(), opportunityApi.listTasks(tenantId)])
      .then(([profile, tasks]) => {
        if (!active) return
        setShellData({ tenantId, user: profile.user, taskCount: tasks.length })
      })
      .catch(() => {
        if (active) setShellData({ tenantId, user: null, taskCount: 0 })
      })
    return () => { active = false }
  }, [tenantId])

  async function endSession() {
    try {
      const redirected = await signOut()
      if (!redirected) navigate('/sign-in', { replace: true })
    } catch (failure) {
      notifications.failure(humanMessage(failure))
    }
  }

  const routeKey = `${location.pathname}${location.search}`
  return <CampaignFlowProvider routeKey={routeKey}><div
    className="app-shell approved-shell approved-shell--workspace">
    <a className="skip-link" href="#main-content">Skip to main content</a>
    <aside className="approved-sidebar">
      <Wordmark />
      <WorkspaceCard workspace={selected} />
      <Navigation roleCode={selected?.roleCode} pathname={location.pathname} taskCount={taskCount} />
      <div className="approved-sidebar-spacer" />
      <article className="approved-assistant-card">
        <span>✦</span><div><strong>Adverti Assistant</strong><small>Your AI co-pilot</small><em>● Online</em></div><b>›</b>
      </article>
    </aside>
    <div className="approved-application-column">
      <GlobalTopbar workspace={selected} user={user} notificationCount={taskCount}
        onSignOut={() => void endSession()} />
      <CampaignFlowRail pathname={location.pathname} />
      <main ref={mainContentRef} className="page-frame approved-page-frame" id="main-content" tabIndex={-1}>
        <Outlet />
      </main>
    </div>
  </div></CampaignFlowProvider>
}

function CampaignFlowRail({ pathname }: { pathname: string }) {
  const campaignFlow = useCampaignFlowResolution()
  return <ApprovedFlowRail pathname={pathname} campaignFlow={campaignFlow} />
}
