import type { ReactNode } from 'react'
import { masterDataCodes } from '../generated/master-data-codes'

type ChannelCode = typeof masterDataCodes.channels[keyof typeof masterDataCodes.channels]

const paths: Partial<Record<ChannelCode, ReactNode>> = {
  [masterDataCodes.channels.ooh]: <><rect x="3" y="5" width="18" height="11" rx="1" /><path d="M8 16v5M16 16v5M5 9h14" /></>,
  [masterDataCodes.channels.dooh]: <><rect x="3" y="4" width="18" height="13" rx="1" /><path d="m8 21 4-4 4 4M8 9h8M8 12h5" /></>,
  [masterDataCodes.channels.radio]: <><rect x="3" y="7" width="18" height="12" rx="2" /><circle cx="15.5" cy="13" r="3" /><path d="M6 11h4M6 14h3M8 7l7-4" /></>,
  [masterDataCodes.channels.tv]: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m8 2 4 3 4-3M9 22h6" /></>,
  [masterDataCodes.channels.print]: <><path d="M6 3h12v18H6zM9 7h6M9 11h6M9 15h4" /></>,
  [masterDataCodes.channels.digital]: <><rect x="4" y="3" width="16" height="18" rx="2" /><path d="M8 7h8M8 11h5M10 18h4" /></>,
  [masterDataCodes.channels.social]: <><circle cx="7" cy="12" r="3" /><circle cx="17" cy="7" r="3" /><circle cx="17" cy="17" r="3" /><path d="m9.5 10.5 5-2M9.5 13.5l5 2" /></>,
  [masterDataCodes.channels.influencer]: <><circle cx="12" cy="8" r="4" /><path d="M5 21a7 7 0 0 1 14 0M18 4l1 1 2-2" /></>,
  [masterDataCodes.channels.experiential]: <><path d="M4 19V9l8-6 8 6v10M8 19v-6h8v6M3 21h18" /></>,
  [masterDataCodes.channels.podcast]: <><circle cx="12" cy="9" r="3" /><path d="M7 9a5 5 0 0 0 10 0M5 9a7 7 0 0 0 14 0M12 16v5" /></>,
  [masterDataCodes.channels.retail]: <><path d="M4 9h16l-1-5H5L4 9ZM6 9v11h12V9M9 20v-6h6v6" /></>,
  [masterDataCodes.channels.transit]: <><rect x="4" y="4" width="16" height="15" rx="3" /><path d="M7 8h10M7 13h10M8 19l-2 3M16 19l2 3" /></>,
  [masterDataCodes.channels.mall]: <><path d="M4 21V7h16v14M8 7V3h8v4M8 11h2M14 11h2M8 15h2M14 15h2" /></>,
  [masterDataCodes.channels.email]: <><rect x="3" y="5" width="18" height="14" rx="2" /><path d="m4 7 8 6 8-6" /></>,
  [masterDataCodes.channels.mobile]: <><rect x="7" y="2" width="10" height="20" rx="2" /><path d="M10 5h4M11 19h2" /></>,
}

export function MediaTypeIcon({ channel }: { channel: string }) {
  const path = paths[channel as ChannelCode] ?? <><circle cx="12" cy="12" r="8" /><path d="M8 12h8" /></>
  return <svg className="media-type-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">{path}</svg>
}
