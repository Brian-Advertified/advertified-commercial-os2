import { useEffect, useRef } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import type { Workspace } from '../api/schemas'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { bookingViewerRoles } from '../booking/booking-roles'
import {
  campaignViewerRoles,
  deliveryProofSubmitterRoles,
} from '../campaign/campaign-roles'
import { fundingViewerRoles } from '../funding/funding-roles'
import { masterDataCodes } from '../generated/master-data-codes'
import { marketplaceViewerRoles } from '../marketplace/marketplace-roles'
import { notifications } from '../notifications/notifications'
import { humanizeCode } from '../presentation/format'
import { Icon, type IconName } from './Icon'

type Destination = {
  to: string
  label: string
  icon: IconName
}

type DestinationGroup = {
  label: string
  items: readonly Destination[]
}

const destinationGroups: readonly DestinationGroup[] = [
  {
    label: 'Work',
    items: [
      { to: '/home', label: 'Overview', icon: 'home' },
      { to: '/opportunities', label: 'Opportunities', icon: 'target' },
      { to: '/tasks', label: 'Assigned tasks', icon: 'tasks' },
    ],
  },
  {
    label: 'Plan and supply',
    items: [
      { to: '/inventory', label: 'Inventory', icon: 'inventory' },
      { to: '/marketplace', label: 'Marketplace', icon: 'marketplace' },
      { to: '/ooh-inbox', label: 'OOH proposal inbox', icon: 'inbox' },
    ],
  },
  {
    label: 'Delivery',
    items: [
      { to: '/funding', label: 'Funding', icon: 'money' },
      { to: '/bookings', label: 'Bookings', icon: 'reservation' },
      { to: '/campaigns', label: 'Campaigns', icon: 'plan' },
      { to: '/delivery-proof-requests', label: 'Proof requests', icon: 'evidence' },
    ],
  },
  {
    label: 'Workspace',
    items: [
      { to: '/admin/commercial', label: 'Commercial settings', icon: 'commercial' },
      { to: '/profile', label: 'Profile', icon: 'profile' },
    ],
  },
]

const oohInboxRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.internalPlanner,
  masterDataCodes.roles.agencyAdmin,
])

const commercialAdministratorRoles = new Set<string>([
  masterDataCodes.roles.platformAdmin,
  masterDataCodes.roles.agencyAdmin,
])

const destinationRoles: Readonly<Record<string, ReadonlySet<string>>> = {
  '/ooh-inbox': oohInboxRoles,
  '/marketplace': marketplaceViewerRoles,
  '/funding': fundingViewerRoles,
  '/bookings': bookingViewerRoles,
  '/campaigns': campaignViewerRoles,
  '/delivery-proof-requests': deliveryProofSubmitterRoles,
  '/admin/commercial': commercialAdministratorRoles,
}

function PrimaryNavigation({ roleCode }: { roleCode?: string }) {
  return <nav className="primary-navigation" aria-label="Workspace navigation">
    {destinationGroups.map(group => {
      const visible = group.items.filter(item =>
        destinationRoles[item.to]?.has(roleCode ?? '') ?? true)
      if (visible.length === 0) return null
      return <section className="navigation-group" key={group.label}>
        <p>{group.label}</p>
        <div>{visible.map(item => <NavLink key={item.to}
          className={({ isActive }) => `nav-link${isActive ? ' nav-link-active' : ''}`}
          to={item.to} aria-label={item.label}>
          <Icon name={item.icon} /><span>{item.label}</span>
        </NavLink>)}</div>
      </section>
    })}
  </nav>
}

const pageContextPrefixes: ReadonlyArray<readonly [string, string]> = [
  ['/briefs/new', 'New campaign Brief'],
  ['/briefs/', 'Campaign Brief'],
  ['/planning/', 'Media planning'],
  ['/proposals/', 'Client proposal'],
  ['/campaigns/', 'Campaign delivery'],
  ['/creative-assets/', 'Supplier creative review'],
  ['/delivery-proofs/', 'Delivery proof review'],
  ['/performance-evidence/', 'Performance evidence review'],
  ['/measurement-reports/', 'Measurement report review'],
  ['/opportunities/', 'Opportunity workspace'],
  ['/strategies/', 'Strategy review'],
  ['/inventory/imports/', 'Inventory review'],
  ['/inventory/products/', 'Inventory detail'],
]

const exactPageContexts: Readonly<Record<string, string>> = {
  '/home': 'Work overview',
  '/opportunities': 'Opportunities',
  '/inventory': 'Media inventory',
  '/marketplace': 'Supplier marketplace',
  '/ooh-inbox': 'OOH proposal inbox',
  '/funding': 'Funding and purchase orders',
  '/bookings': 'Bookings',
  '/campaigns': 'Campaign delivery',
  '/delivery-proof-requests': 'Supplier proof requests',
  '/tasks': 'Assigned tasks',
  '/admin/commercial': 'Commercial settings',
  '/profile': 'Profile',
  '/workspaces': 'Workspaces',
}

function pageContext(pathname: string) {
  const prefix = pageContextPrefixes.find(([value]) => pathname.startsWith(value))
  return prefix?.[1] ?? exactPageContexts[pathname] ?? 'Advertified'
}

function WorkspaceIdentity({ workspace }: { workspace: Workspace | null }) {
  const initial = workspace ? workspace.name.trim().charAt(0).toUpperCase() : 'A'
  return <footer className="workspace-identity">
    <span className="workspace-initial" aria-hidden="true">{initial}</span>
    <span><small>Current workspace</small>
      <strong>{workspace ? workspace.name : 'Choose a workspace'}</strong>
      {workspace && <em>{humanizeCode(workspace.roleCode, true)}</em>}
    </span>
  </footer>
}

function Topbar({ pathname, workspace, onSignOut }: {
  pathname: string
  workspace: Workspace | null
  onSignOut: () => void
}) {
  return <header className="topbar">
    <div className="topbar-context">
      <p className="eyebrow">Workspace</p>
      <strong>{pageContext(pathname)}</strong>
    </div>
    <div className="topbar-actions">
      <span className="topbar-workspace"><small>Current workspace</small>
        <strong>{workspace ? workspace.name : 'Choose a workspace'}</strong></span>
      {import.meta.env.DEV && <span className="development-pill">Development</span>}
      <NavLink className="text-action" to="/workspaces">
        <Icon name="switch" /> Switch workspace
      </NavLink>
      <button className="text-action" type="button" onClick={onSignOut}>Sign out</button>
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

  useEffect(() => {
    if (previousPath.current !== location.pathname) {
      mainContentRef.current?.focus()
    }
    previousPath.current = location.pathname
  }, [location.pathname])

  async function endSession() {
    try {
      const redirected = await signOut()
      if (!redirected) {
        navigate('/sign-in', { replace: true })
      }
    } catch (failure) {
      notifications.failure(humanMessage(failure))
    }
  }

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Skip to main content</a>
      <aside className="navigation-rail" aria-label="Primary navigation">
        <NavLink className="shell-brand" to="/home" aria-label="Advertified home">
          <span className="brand-mark">A</span>
          <span><strong>Advertified <em>OS</em></strong><small>Commercial intelligence</small></span>
        </NavLink>
        <NavLink className="new-brief-action" to="/briefs/new" aria-label="New campaign Brief">
          <Icon name="plus" /><span>New campaign Brief</span>
        </NavLink>
        <PrimaryNavigation roleCode={selected ? selected.roleCode : undefined} />
        <WorkspaceIdentity workspace={selected} />
      </aside>

      <div className="application-column">
        <Topbar pathname={location.pathname} workspace={selected}
          onSignOut={() => void endSession()} />
        <main ref={mainContentRef} className="page-frame" id="main-content" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}
