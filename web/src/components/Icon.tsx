import type { ReactNode } from 'react'

export type IconName =
  | 'home' | 'target' | 'brief' | 'inventory' | 'inbox' | 'marketplace'
  | 'reservation' | 'commercial' | 'tasks' | 'bell' | 'profile' | 'switch'
  | 'arrow' | 'shield' | 'plus' | 'search' | 'evidence' | 'plan'
  | 'proposal' | 'calendar' | 'chart' | 'users' | 'globe' | 'money'

const paths: Record<IconName, ReactNode> = {
  home: <><path d="M3 10.5 12 3l9 7.5" /><path d="M5.5 9.5V21h13V9.5M9 21v-7h6v7" /></>,
  target: <><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="3" /><path d="M12 2v3M22 12h-3M12 22v-3M2 12h3" /></>,
  brief: <><path d="M6 3h9l3 3v15H6z" /><path d="M15 3v4h4M9 11h6M9 15h6" /></>,
  inventory: <><rect x="3" y="4" width="8" height="7" rx="1" /><rect x="13" y="4" width="8" height="7" rx="1" /><rect x="3" y="13" width="8" height="7" rx="1" /><rect x="13" y="13" width="8" height="7" rx="1" /></>,
  inbox: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m4 7 8 6 8-6" /></>,
  marketplace: <><path d="M4 9h16l-1-5H5L4 9Z" /><path d="M6 9v11h12V9M9 20v-6h6v6" /></>,
  reservation: <><rect x="4" y="5" width="16" height="16" rx="2" /><path d="M8 3v4M16 3v4M4 10h16M8 14h3M8 18h6" /></>,
  commercial: <><circle cx="12" cy="12" r="9" /><path d="M15.5 8.5c-.8-.8-2-1.2-3.5-1.2-2 0-3.5 1-3.5 2.5 0 3.7 7 1.4 7 5 0 1.4-1.5 2.4-3.5 2.4-1.5 0-2.9-.5-3.8-1.4M12 5v14" /></>,
  tasks: <><path d="M9 6h11M9 12h11M9 18h11" /><path d="m3.5 6 1 1 2-2M3.5 12l1 1 2-2M3.5 18l1 1 2-2" /></>,
  bell: <><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9" /><path d="M10 21h4" /></>,
  profile: <><circle cx="12" cy="8" r="4" /><path d="M4 21a8 8 0 0 1 16 0" /></>,
  switch: <><path d="M7 7h13l-3-3M17 17H4l3 3" /></>,
  arrow: <><path d="M5 12h14M14 7l5 5-5 5" /></>,
  shield: <><path d="M12 3 4.5 6v5.5c0 4.5 3 7.5 7.5 9.5 4.5-2 7.5-5 7.5-9.5V6L12 3Z" /><path d="m9 12 2 2 4-4" /></>,
  plus: <><path d="M12 5v14M5 12h14" /></>,
  search: <><circle cx="11" cy="11" r="7" /><path d="m16 16 5 5" /></>,
  evidence: <><path d="M5 4h14v16H5z" /><path d="m8 12 2 2 5-5M8 17h8" /></>,
  plan: <><path d="M4 19V5M4 19h16" /><path d="m7 15 4-5 3 2 5-7" /></>,
  proposal: <><path d="M6 3h9l3 3v15H6z" /><path d="M15 3v4h4M9 12h6M9 16h4" /><circle cx="17" cy="17" r="3" /></>,
  calendar: <><rect x="4" y="5" width="16" height="16" rx="2" /><path d="M8 3v4M16 3v4M4 10h16" /></>,
  chart: <><path d="M4 20V10M10 20V4M16 20v-7M22 20V7" /></>,
  users: <><circle cx="9" cy="8" r="3" /><circle cx="17" cy="9" r="2.5" /><path d="M3 20a6 6 0 0 1 12 0M14 20a5 5 0 0 1 7 0" /></>,
  globe: <><circle cx="12" cy="12" r="9" /><path d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18" /></>,
  money: <><rect x="3" y="6" width="18" height="12" rx="2" /><circle cx="12" cy="12" r="3" /><path d="M7 9H6v1M17 15h1v-1" /></>,
}

export function Icon({ name }: { name: IconName }) {
  return (
    <svg className="icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      {paths[name]}
    </svg>
  )
}
