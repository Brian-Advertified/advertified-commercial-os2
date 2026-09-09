import type { BuyAssessment } from '../api/buy-assessment-schema'
import { plannerReasoningContent as copy } from './planner-reasoning-content'

export function InventoryPlannerReasoning({ value }: { value: NonNullable<BuyAssessment['plannerReasoning']> }) {
  return <section className="buy-assessment" aria-label={copy.title}>
    <h4>{copy.title}</h4><p><strong>{copy.role}: </strong>{value.plannedChannelRole || copy.unknown}</p>
    <p>{copy.coverage}: {value.requiredPlacesMatched} / {value.requiredPlacesTotal}</p>
    <p>{value.hasMeasuredTargetAudience ? copy.target : copy.noTarget}</p>
    <h4>{copy.audience}</h4><dl className="buy-assessment-facts">{value.targetContexts.map((target, index) =>
      <div key={index}><dt>{target.name}</dt><dd>{target.needState || copy.unknown}<br />{target.buyingContext || copy.unknown}</dd></div>)}</dl>
    <h4>{copy.supported}</h4>{value.supportedReasons.length ? <Reasons values={value.supportedReasons} /> : <p>{copy.noReasons}</p>}
    {value.buyingWarnings.length > 0 && <><h4>{copy.warnings}</h4><Reasons values={value.buyingWarnings} /></>}
    <h4>{copy.questions}</h4><Reasons values={value.reviewQuestions} />
  </section>
}

function Reasons({ values }: { values: string[] }) {
  return <ul>{values.map((key, index) => <li key={index}>{copy.messages[key] ?? copy.unsupported}</li>)}</ul>
}
