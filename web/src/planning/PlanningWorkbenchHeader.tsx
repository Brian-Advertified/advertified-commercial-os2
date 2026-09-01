import type { MediaMix, MediaPlan, PlanningWorkspace, Shortlist } from '../api/planning-schemas'
import { PlanningStageRail, type PlanningStageId } from '../components/CampaignStageRail'
import { masterDataCodes } from '../generated/master-data-codes'
import { formatMoney, humanizeCode } from '../presentation/format'

type PlanningHeaderProps = {
  workspace: PlanningWorkspace
  mix: MediaMix | null
  shortlist: Shortlist | null
  plan: MediaPlan | null
}

type SnapshotValue = {
  label: string
  value: string
  note: string
}

export function PlanningWorkbenchHeader(props: PlanningHeaderProps) {
  const progress = planningProgress(props)
  return <>
    <PlanningHero {...props} />
    <PlanningStageRail current={progress.current} completed={progress.completed} />
    <PlanningWorkspaceNavigation {...props} />
  </>
}

function PlanningHero({ workspace, mix, shortlist, plan }: PlanningHeaderProps) {
  const snapshots = [
    modeSnapshot(workspace),
    investmentSnapshot(mix),
    supplySnapshot(shortlist),
    planSnapshot(plan),
  ]
  return <header className="planning-workbench-hero"><div>
    <p className="eyebrow eyebrow-light">{workspace.clientName} · Campaign planning</p>
    <h1 id="planning-title">Planning workbench</h1>
    <p>Audience, allocation, verified supply and the reconciled media plan remain in one governed workspace.</p>
  </div><dl className="planning-snapshot">{snapshots.map(snapshot =>
    <Snapshot key={snapshot.label} {...snapshot} />)}</dl></header>
}

function Snapshot({ label, value, note }: SnapshotValue) {
  return <div><dt>{label}</dt><dd>{value}</dd><small>{note}</small></div>
}

function modeSnapshot(workspace: PlanningWorkspace): SnapshotValue {
  if (!workspace.campaignMode) return {
    label: 'Media scope',
    value: 'Needs confirmation',
    note: 'Only an unclear requirement needs a decision',
  }
  return {
    label: 'Media scope',
    value: workspace.campaignMode.mode === masterDataCodes.campaignModes.oohOnly
      ? 'OOH / DOOH' : 'Full campaign',
    note: 'Locked for this campaign',
  }
}

function investmentSnapshot(mix: MediaMix | null): SnapshotValue {
  if (!mix) return {
    label: 'Investment',
    value: 'Not allocated',
    note: 'Create the media mix',
  }
  const label = mix.allocations.length === 1 ? 'media channel' : 'media channels'
  return {
    label: 'Investment',
    value: formatMoney(mix.totalBudgetMinor, mix.currency),
    note: `${mix.allocations.length} ${label}`,
  }
}

function supplySnapshot(shortlist: Shortlist | null): SnapshotValue {
  if (!shortlist) return {
    label: 'Selected supply',
    value: 'Not shortlisted',
    note: 'Inventory follows mix approval',
  }
  const selected = shortlist.candidates.filter(item => item.isSelected).length
  return {
    label: 'Selected supply',
    value: String(selected),
    note: `${shortlist.candidates.length} candidates reviewed`,
  }
}

function planSnapshot(plan: MediaPlan | null): SnapshotValue {
  if (!plan) return {
    label: 'Plan status',
    value: 'Not created',
    note: 'Reconcile selected inventory',
  }
  const unresolved = plan.objections.filter(item => !item.resolution).length
  const objectionLabel = unresolved === 1 ? 'objection' : 'objections'
  return {
    label: 'Plan status',
    value: humanizeCode(plan.status, true),
    note: `${unresolved} unresolved ${objectionLabel}`,
  }
}

function PlanningWorkspaceNavigation({ workspace, mix, shortlist, plan }: PlanningHeaderProps) {
  const items = [
    { href: '#audience-section', label: 'Audience', ready: Boolean(workspace.audience) },
    { href: '#media-mix', label: 'Media mix', ready: Boolean(mix) },
    { href: '#inventory-selection', label: 'Inventory', ready: Boolean(shortlist) },
    { href: '#media-plan', label: 'Media plan', ready: Boolean(plan) },
  ]
  return <nav className="planning-workspace-navigation" aria-label="Planning sections">
    {items.map(item => <a href={item.href} key={item.href}
      className={item.ready ? 'is-ready' : ''}>
      <span>{item.ready ? '✓' : '○'}</span>{item.label}
    </a>)}
  </nav>
}

function planningProgress({ workspace, mix, shortlist, plan }: PlanningHeaderProps) {
  const states: ReadonlyArray<readonly [PlanningStageId, boolean]> = [
    ['audienceStage', workspace.audience?.status === masterDataCodes.lifecycleStatuses.approved],
    ['media-mix', mix?.status === masterDataCodes.lifecycleStatuses.approved],
    ['inventory', shortlist?.status === masterDataCodes.lifecycleStatuses.approved],
    ['media-plan', plan?.status === masterDataCodes.lifecycleStatuses.approved],
  ]
  const completed = new Set<PlanningStageId>([
    'brief',
    ...states.filter(([, isComplete]) => isComplete).map(([stage]) => stage),
  ])
  const current = states.find(([, isComplete]) => !isComplete)?.[0] ?? null
  return { completed, current }
}
