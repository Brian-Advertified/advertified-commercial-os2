import { Link } from 'react-router-dom'
import type { CampaignFlowResolution } from '../campaign-flow/campaign-flow-state'
import { masterDataCodes } from '../generated/master-data-codes'

type Step = {
  label: string
  copy: string
}

const inventorySteps: readonly Step[] = [
  { label: 'Import Sources', copy: 'Upload inventory' },
  { label: 'Classify Source', copy: 'Understand structure' },
  { label: 'Extract Candidates', copy: 'Create records' },
  { label: 'Validate & Reconcile', copy: 'Check source truth' },
  { label: 'Human Review', copy: 'Resolve exceptions' },
  { label: 'Publish Inventory', copy: 'Make supply usable' },
  { label: 'Benchmark & Use', copy: 'Plan with evidence' },
]

export function ApprovedFlowRail({ pathname, hash, campaignFlow }: {
  pathname: string
  hash?: string
  campaignFlow: CampaignFlowResolution
}) {
  if (pathname === '/home' || pathname === '/workspaces' || pathname === '/profile') return null
  if (pathname.startsWith('/inventory/imports/')) return <Rail
    ariaLabel="Inventory lifecycle"
    steps={inventorySteps}
    active={inventoryIndex(pathname)}
    mode="inventory"
  />
  if (!isCampaignFlow(pathname)) return null
  const mode = resolvedMode(campaignFlow)
  return <Rail
    ariaLabel={mode === masterDataCodes.campaignModes.oohOnly
      ? 'Outdoor advertising campaign'
      : mode === masterDataCodes.campaignModes.fullCampaign
        ? 'Full Campaign Flow'
        : 'Campaign Flow'}
    steps={campaignSteps(mode)}
    active={campaignIndex(pathname, hash ?? '')}
    mode={mode ?? undefined}
    exitTo="/campaigns"
  />
}

function campaignSteps(mode: string | null): readonly Step[] {
  const planningStep = mode === masterDataCodes.campaignModes.oohOnly
    ? { label: 'Inventory', copy: 'Find & short-list' }
    : { label: 'Media Plan', copy: 'Select channels & partners' }
  return [
    { label: 'Brief', copy: 'Tell us what you need' },
    { label: 'AI Interpretation', copy: 'We analyse your brief' },
    { label: 'Audience & STP', copy: 'Find the right people' },
    { label: 'Strategy', copy: 'Recommendations' },
    planningStep,
    { label: 'Proposal', copy: 'Review & refine' },
    { label: 'Launch', copy: 'Bring it to life' },
  ]
}

function resolvedMode(flow: CampaignFlowResolution): string | null {
  return flow.status === 'resolved' ? flow.mode : null
}

function isCampaignFlow(pathname: string) {
  if (pathname === '/ooh-inbox' || pathname === '/funding' || pathname.startsWith('/funding?')) return true
  const area = pathname.split('/').filter(Boolean)[0] ?? ''
  return [
    'briefs', 'stp', 'planning', 'marketplace', 'proposals', 'bookings', 'campaigns',
    'creative-assets', 'delivery-proofs', 'performance-evidence', 'measurement-reports',
  ].includes(area)
}

function campaignIndex(pathname: string, hash: string) {
  if (pathname === '/briefs/new') return hash === '#interpretation' ? 1 : 0
  if (/^\/briefs\/[^/]+\/proposals\/new$/.test(pathname)) return 5
  if (pathname.startsWith('/briefs/')) return hash === '#interpretation' ? 1 : 0
  if (pathname.startsWith('/planning/')) return hash === '#strategy' ? 3 : 4
  const match = campaignStagePrefixes.find(item => item.prefixes.some(prefix => pathname.startsWith(prefix)))
  return match?.index ?? 0
}

const campaignStagePrefixes = [
  { index: 2, prefixes: ['/stp/'] },
  { index: 4, prefixes: ['/marketplace'] },
  { index: 5, prefixes: ['/proposals'] },
  { index: 6, prefixes: ['/bookings', '/funding', '/campaigns', '/creative-assets/', '/delivery-proofs/'] },
] as const

function inventoryIndex(pathname: string) {
  return pathname.includes('/products/') ? 6 : pathname.includes('/imports/') ? 4 : 0
}

function Rail({ ariaLabel, steps, active, mode, exitTo }: {
  ariaLabel: string
  steps: readonly Step[]
  active: number
  mode?: string
  exitTo?: string
}) {
  return <section className="approved-flow-rail" aria-label={ariaLabel} data-campaign-mode={mode}>
    <ol>{steps.map((step, index) => <li key={step.label}
      className={index === active ? 'is-active' : index < active ? 'is-complete' : ''}
      aria-current={index === active ? 'step' : undefined}>
      <span className="approved-flow-step-icon" data-step-number={index + 1} />
      <span className="approved-flow-step-label">{step.label}</span>
      <span className="approved-flow-step-copy">{step.copy}</span>
    </li>)}</ol>
    {exitTo && <Link className="approved-flow-exit" to={exitTo}>Save &amp; Exit</Link>}
  </section>
}
