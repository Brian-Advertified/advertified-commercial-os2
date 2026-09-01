import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { Icon } from '../components/Icon'
import {
  campaignModeIcon,
  campaignModeLabel,
  suppliedList,
  suppliedText,
  understandingBudgetLabel,
  understandingTaxLabel,
} from './brief-intake-presentation'

export function BriefUnderstandingSummary({ understanding, compact = false }: {
  understanding: SuppliedBriefUnderstanding
  compact?: boolean
}) {
  const draft = understanding.draft
  const className = compact
    ? 'understanding-summary understanding-summary-compact'
    : 'understanding-summary'
  return <section className={className}>
    <header><div><p className="eyebrow">Current understanding</p>
      <h2>{understanding.title}</h2><p>{understanding.clientName ?? 'Client still to be confirmed'}</p></div>
      <span className="understanding-mode-chip"><Icon name={campaignModeIcon(
        understanding.campaignMode)} />{campaignModeLabel(understanding.campaignMode)}</span></header>
    <dl>
      <div><dt>Objective</dt><dd>{suppliedText(draft.objective)}</dd></div>
      <div><dt>Audience</dt><dd>{suppliedList(draft.audiences)}</dd></div>
      <div><dt>Geography</dt><dd>{suppliedList(draft.geographies)}</dd></div>
      <div><dt>Timing</dt><dd>{suppliedText(draft.timing)}</dd></div>
      <div><dt>Budget</dt><dd>{understandingBudgetLabel(understanding)}</dd></div>
      <div><dt>Tax treatment</dt><dd>{understandingTaxLabel(understanding)}</dd></div>
    </dl>
  </section>
}
