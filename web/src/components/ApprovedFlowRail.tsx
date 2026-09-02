import { Icon, type IconName } from './Icon'
import type { CampaignFlowResolution } from '../campaign-flow/campaign-flow-state'
import { masterDataCodes } from '../generated/master-data-codes'

type Step = { label: string; icon: IconName }

const campaignSteps: readonly Step[] = [
  { label: 'Brief', icon: 'brief' },
  { label: 'Strategy & STP', icon: 'target' },
  { label: 'Media Plan', icon: 'plan' },
  { label: 'Proposal', icon: 'proposal' },
  { label: 'Client Decision', icon: 'shield' },
  { label: 'Funding', icon: 'money' },
  { label: 'Booking', icon: 'reservation' },
  { label: 'Readiness & Live', icon: 'arrow' },
  { label: 'Proof', icon: 'evidence' },
  { label: 'Measurement & Learning', icon: 'chart' },
]

const inventorySteps: readonly Step[] = [
  { label: 'Import Sources', icon: 'plus' },
  { label: 'Classify & Render', icon: 'brief' },
  { label: 'Extract Candidates', icon: 'search' },
  { label: 'Validate & Reconcile', icon: 'shield' },
  { label: 'Human Review', icon: 'tasks' },
  { label: 'Publish Inventory', icon: 'inventory' },
  { label: 'Benchmark & Use', icon: 'chart' },
]

export function ApprovedFlowRail({ pathname, campaignFlow }: {
  pathname: string
  campaignFlow: CampaignFlowResolution
}) {
  if (pathname === '/home' || pathname === '/workspaces' || pathname === '/profile') return null
  if (pathname.startsWith('/inventory/imports/')) return null
  if (pathname.startsWith('/inventory')) {
    const active = pathname.includes('/products/') ? 6 : 5
    return <Rail title="Inventory Intelligence Flow"
      subtitle="From supplier files to commercially usable inventory"
      steps={inventorySteps} active={active} tone="purple" />
  }
  if (isCampaignFlow(pathname) && campaignFlow.status !== 'unbound') {
    const presentation = campaignPresentation(campaignFlow)
    return <Rail title={presentation.title} subtitle={presentation.subtitle}
      steps={campaignSteps} active={campaignIndex(pathname)}
      tone={presentation.tone} mode={presentation.mode} />
  }
  return null
}

function campaignPresentation(flow: CampaignFlowResolution) {
  if (flow.status === 'resolved' &&
      flow.mode === masterDataCodes.campaignModes.oohOnly) {
    return { title: 'OOH-only Campaign Flow',
      subtitle: 'OOH and DOOH only · One governed campaign lifecycle',
      tone: 'green' as const, mode: flow.mode }
  }
  if (flow.status === 'resolved' &&
      flow.mode === masterDataCodes.campaignModes.fullCampaign) {
    return { title: 'Full Campaign Flow',
      subtitle: 'Full channel registry · One governed campaign lifecycle',
      tone: 'purple' as const, mode: flow.mode }
  }
  if (flow.status === 'unavailable') {
    return { title: 'Campaign Flow', subtitle: 'Campaign type could not be verified',
      tone: 'purple' as const, mode: 'mode-unavailable' }
  }
  if (flow.status === 'loading') {
    return { title: 'Campaign Flow', subtitle: 'Verifying campaign type…',
      tone: 'purple' as const, mode: 'mode-loading' }
  }
  return { title: 'Campaign Flow',
    subtitle: 'Campaign type must be confirmed before planning',
    tone: 'purple' as const, mode: 'mode-unresolved' }
}

function isCampaignFlow(pathname: string) {
  if (pathname === '/ooh-inbox' || pathname === '/funding' ||
      pathname.startsWith('/funding?')) return true
  const parts = pathname.split('/').filter(Boolean)
  if (['briefs', 'stp', 'planning', 'proposals', 'bookings', 'campaigns'].includes(
      parts[0] ?? '')) return parts.length > 1
  return [
    'creative-assets',
    'delivery-proofs',
    'performance-evidence',
    'measurement-reports',
  ].includes(parts[0] ?? '')
}

const campaignStageByArea: Readonly<Record<string, number>> = {
  briefs: 0,
  stp: 1,
  planning: 2,
  proposals: 3,
  funding: 5,
  bookings: 6,
  campaigns: 7,
  'creative-assets': 7,
  'delivery-proofs': 8,
  'performance-evidence': 9,
  'measurement-reports': 9,
}

function campaignIndex(pathname: string) {
  if (pathname.includes('/proposals/new')) return 3
  if (pathname.endsWith('/delivery-proof/new')) return 8
  if (pathname === '/ooh-inbox') return 0
  const area = pathname.split('/').filter(Boolean)[0] ?? ''
  return campaignStageByArea[area] ?? 0
}

function Rail({ title, subtitle, steps, active, tone, mode }: {
  title: string
  subtitle: string
  steps: readonly Step[]
  active: number
  tone: 'purple' | 'green'
  mode?: string
}) {
  return <section className={`approved-flow-rail approved-flow-rail--${tone}`}
    aria-label={title} data-campaign-mode={mode}>
    <div className="approved-flow-title"><h1>{title}</h1><p>{subtitle}</p></div>
    <ol>{steps.map((step, index) => <li key={step.label}
      className={index === active ? 'is-active' : index < active ? 'is-complete' : ''}>
      <span className="approved-flow-step-icon">{index < active ? '✓' : <Icon name={step.icon} />}</span>
      <span>{step.label}</span>
    </li>)}</ol>
  </section>
}
