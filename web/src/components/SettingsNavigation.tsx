import { NavLink } from 'react-router-dom'

const destinations = [
  ['/admin/commercial', 'Commercial policy'],
  ['/admin/agents', 'Agent operations'],
] as const

export function SettingsNavigation() {
  return <nav className="settings-navigation" aria-label="Settings sections">
    {destinations.map(([to, label]) => <NavLink key={to} to={to}
      className={({ isActive }) => isActive ? 'is-active' : undefined}>{label}</NavLink>)}
  </nav>
}
