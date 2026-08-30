import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { humanMessage } from '../api/client'
import { useSession } from '../auth/session-state'
import { useWorkspace } from '../auth/workspace-state'
import { notifications } from '../notifications/notifications'
import { masterDataCodes } from '../generated/master-data-codes'
import { marketplaceViewerRoles } from '../marketplace/marketplace-roles'
import { bookingViewerRoles } from '../booking/booking-roles'
import { Icon } from './Icon'

const destinations = [
  { to: '/home', label: 'Home', icon: 'home', enabled: true },
  { to: '/opportunities', label: 'Opportunities', icon: 'tasks', enabled: true },
  { to: '/briefs/new', label: 'Briefs', icon: 'tasks', enabled: true },
  { to: '/inventory', label: 'Inventory', icon: 'tasks', enabled: true },
  { to: '/ooh-inbox', label: 'Proposal inbox', icon: 'mail', enabled: true },
  { to: '/marketplace', label: 'Marketplace', icon: 'tasks', enabled: true },
  { to: '/bookings', label: 'Bookings', icon: 'tasks', enabled: true },
  { to: '/admin/commercial', label: 'Commercial', icon: 'tasks', enabled: true },
  { to: '/tasks', label: 'Tasks', icon: 'tasks', enabled: true },
  { to: '/notifications', label: 'Notifications', icon: 'bell', enabled: false },
  { to: '/profile', label: 'Profile', icon: 'profile', enabled: true },
] as const

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
  '/bookings': bookingViewerRoles,
  '/admin/commercial': commercialAdministratorRoles,
}

function PrimaryNavigation({ roleCode }: { roleCode?: string }) {
  const visible = destinations.filter((item) =>
    destinationRoles[item.to]?.has(roleCode ?? '') ?? true)
  return <nav>{visible.map((item) => item.enabled ? (
    <NavLink key={item.to}
      className={({ isActive }) => `nav-link${isActive ? ' nav-link-active' : ''}`}
      to={item.to} aria-label={item.label}>
      <Icon name={item.icon} /><span>{item.label}</span>
    </NavLink>
  ) : (
    <span className="nav-link nav-link-disabled"
      title="Available when its real workflow is implemented"
      aria-disabled="true" key={item.to}>
      <Icon name={item.icon} /><span>{item.label}</span>
    </span>
  ))}</nav>
}

export function AppShell() {
  const { signOut } = useSession()
  const { selected } = useWorkspace()
  const navigate = useNavigate()

  async function endSession() {
    try {
      await signOut()
      navigate('/sign-in', { replace: true })
    } catch (failure) {
      notifications.failure(humanMessage(failure))
    }
  }

  return (
    <div className="app-shell">
      <aside className="navigation-rail" aria-label="Primary navigation">
        <NavLink className="brand-mark" to="/home" aria-label="Advertified home">A</NavLink>
        <PrimaryNavigation roleCode={selected?.roleCode} />
        <span className="environment-pill">Local only</span>
      </aside>

      <div className="application-column">
        <header className="topbar">
          <div>
            <p className="eyebrow">Current workspace</p>
            <strong>{selected?.name ?? 'Choose a workspace'}</strong>
          </div>
          <div className="topbar-actions">
            <NavLink className="text-action" to="/workspaces">
              <Icon name="switch" /> Switch
            </NavLink>
            <button className="text-action" type="button" onClick={() => void endSession()}>
              Sign out
            </button>
          </div>
        </header>
        <main className="page-frame" id="main-content"><Outlet /></main>
      </div>
    </div>
  )
}
