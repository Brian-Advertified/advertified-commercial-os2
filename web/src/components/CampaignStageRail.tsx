import { Icon, type IconName } from './Icon'

export type PlanningStageId =
  | 'brief' | 'audienceStage' | 'media-mix' | 'inventory' | 'media-plan'

type PlanningStage = {
  id: PlanningStageId
  label: string
  icon: IconName
}

const stages: readonly PlanningStage[] = [
  { id: 'brief', label: 'Brief', icon: 'brief' },
  { id: 'audienceStage', label: 'Audience', icon: 'users' },
  { id: 'media-mix', label: 'Media mix', icon: 'chart' },
  { id: 'inventory', label: 'Inventory', icon: 'inventory' },
  { id: 'media-plan', label: 'Media plan', icon: 'plan' },
]

export function PlanningStageRail({
  current,
  completed,
}: {
  current: PlanningStageId | null
  completed: ReadonlySet<PlanningStageId>
}) {
  return <div className="campaign-stage-rail">
    <ol aria-label="Planning work stages">{stages.map((stage, index) => {
      const complete = completed.has(stage.id)
      const active = stage.id === current
      const state = complete ? 'complete' : active ? 'current' : 'upcoming'
      return <li className={`campaign-stage campaign-stage-${state}`} key={stage.id}
        aria-current={active ? 'step' : undefined}>
        <span className="campaign-stage-mark">
          {complete ? <span aria-hidden="true">✓</span> : <Icon name={stage.icon} />}
        </span>
        <span><strong>{stage.label}</strong><small>{complete
          ? 'Complete' : active ? 'Current planning stage' : `Step ${index + 1}`}</small></span>
      </li>
    })}</ol>
  </div>
}
