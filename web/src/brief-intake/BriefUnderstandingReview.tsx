import type { ReactNode } from 'react'
import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { Icon, type IconName } from '../components/Icon'
import { humanizeCode } from '../presentation/format'
import {
  campaignModeLabel,
  suppliedText,
  understandingBudgetLabel,
  understandingTaxLabel,
} from './brief-intake-presentation'

export function BriefUnderstandingReview({
  understanding,
  busy,
  onApprove,
  onEdit,
  onCorrectMode,
}: {
  understanding: SuppliedBriefUnderstanding
  busy: boolean
  onApprove: () => Promise<void>
  onEdit: () => void
  onCorrectMode: () => void
}) {
  return <div className="brief-understanding-review">
    <UnderstandingReviewHero understanding={understanding} />
    <UnderstandingDetailGrid understanding={understanding} />
    <KnowledgePanel understanding={understanding} />
    <ApprovalBar busy={busy} onApprove={onApprove} onEdit={onEdit}
      onCorrectMode={onCorrectMode} />
  </div>
}

function UnderstandingReviewHero({ understanding }: {
  understanding: SuppliedBriefUnderstanding
}) {
  return <header className="understanding-review-hero"><div>
    <p className="eyebrow eyebrow-light">Brief ready for review</p>
    <h2>Confirm what Advertified understood before planning begins.</h2>
    <p>The original source remains unchanged. Approving this view creates the retained Brief version and locks the campaign media scope for planning.</p>
  </div><div className="understanding-decision"><span><Icon name="shield" /> Campaign mode decision</span>
    <strong>{campaignModeLabel(understanding.campaignMode)}</strong>
    <small>Locks when approved · {Math.round(understanding.campaignModeConfidence * 100)}% confidence</small>
    <p>{understanding.campaignModeRationale}</p>
  </div></header>
}

function UnderstandingDetailGrid({ understanding }: {
  understanding: SuppliedBriefUnderstanding
}) {
  const draft = understanding.draft
  return <div className="understanding-detail-grid">
    <ReviewCard eyebrow="Commercial need" title="Business problem" icon="target">
      <p>{suppliedText(draft.businessProblem)}</p>
    </ReviewCard>
    <ReviewCard eyebrow="Campaign outcome" title="Objective" icon="plan">
      <p>{suppliedText(draft.objective)}</p>
    </ReviewCard>
    <ReviewCard eyebrow="People" title="Audience direction" icon="users">
      <ValueList values={draft.audiences} empty="No audience was supplied." />
    </ReviewCard>
    <ReviewCard eyebrow="Place" title="Geography" icon="globe">
      <ValueList values={draft.geographies} empty="No geography was supplied." />
    </ReviewCard>
    <ReviewCard eyebrow="Investment" title="Budget and timing" icon="money">
      <dl className="review-fact-list"><div><dt>Budget</dt><dd>{understandingBudgetLabel(understanding)}</dd></div>
        <div><dt>Timing</dt><dd>{suppliedText(draft.timing)}</dd></div>
        <div><dt>Tax treatment</dt><dd>{understandingTaxLabel(understanding)}</dd></div></dl>
    </ReviewCard>
    <ReviewCard eyebrow="Media direction" title="Requested or implied media" icon="inventory">
      <ValueList values={draft.mediaRequirements} empty="No media channel was specified." />
    </ReviewCard>
    <ReviewCard eyebrow="Boundaries" title="Constraints" icon="shield">
      <ValueList values={draft.constraints} empty="No additional constraints were supplied." />
    </ReviewCard>
    <ReviewCard eyebrow="Success" title="Measurement" icon="chart">
      <ValueList values={draft.measurement} empty="Measurement requirements were not supplied." />
    </ReviewCard>
  </div>
}

function KnowledgePanel({ understanding }: {
  understanding: SuppliedBriefUnderstanding
}) {
  const draft = understanding.draft
  const assumptions = draft.assumptions.map(item =>
    `${item.value} — ${item.validationNeeded}`)
  const unknowns = draft.unknowns.map(item => item.question)
  return <section className="brief-knowledge-panel">
    <header><div><p className="eyebrow">Evidence and uncertainty</p>
      <h2>What is supported, assumed or still unknown</h2></div>
      <span className="status-chip status-neutral">{understanding.evidence.length} source links</span>
    </header><div className="knowledge-columns">
      <KnowledgeList title="Supplied facts" values={draft.facts} tone="fact" />
      <KnowledgeList title="Assumptions to carry" values={assumptions} tone="assumption" />
      <KnowledgeList title="Non-blocking unknowns" values={unknowns} tone="unknown" />
    </div><EvidenceDetails understanding={understanding} />
  </section>
}

function EvidenceDetails({ understanding }: {
  understanding: SuppliedBriefUnderstanding
}) {
  if (understanding.evidence.length === 0) return null
  return <details className="brief-evidence-details">
    <summary>Review source evidence used for this understanding</summary>
    <div>{understanding.evidence.map((item, index) => <article
      key={`${item.fieldPath}-${item.sourceLocator}-${index}`}>
      <span><strong>{humanizeCode(item.fieldPath.replaceAll('.', '_'), true)}</strong>
        <em>{humanizeCode(item.kind, true)} · {Math.round(item.confidence * 100)}%</em></span>
      <blockquote>{item.excerpt || 'The source supports this field without a separate excerpt.'}</blockquote>
    </article>)}</div>
  </details>
}

function ApprovalBar({ busy, onApprove, onEdit, onCorrectMode }: {
  busy: boolean
  onApprove: () => Promise<void>
  onEdit: () => void
  onCorrectMode: () => void
}) {
  return <footer className="brief-approval-bar"><div><Icon name="shield" /><span>
    <strong>Approval creates the planning input.</strong>
    <small>Material changes after approval require a new Brief version.</small></span></div>
    <div><button className="secondary-button" type="button" onClick={onEdit}
      disabled={busy}>Edit source</button>
      <button className="secondary-button" type="button" onClick={onCorrectMode}
        disabled={busy}>Correct media scope</button>
      <button className="primary-button" type="button" onClick={() => void onApprove()}
        disabled={busy}>{busy ? 'Creating the planning workspace…' : 'Approve Brief and start planning'}
        {!busy && <Icon name="arrow" />}</button></div></footer>
}

function ReviewCard({ eyebrow, title, icon, children }: {
  eyebrow: string
  title: string
  icon: IconName
  children: ReactNode
}) {
  return <article className="understanding-review-card"><header><span><Icon name={icon} /></span>
    <div><p>{eyebrow}</p><h3>{title}</h3></div></header>{children}</article>
}

function ValueList({ values, empty }: { values: string[]; empty: string }) {
  return values.length > 0 ? <ul>{values.map(value => <li key={value}>{value}</li>)}</ul>
    : <p className="review-empty-copy">{empty}</p>
}

function KnowledgeList({ title, values, tone }: {
  title: string
  values: string[]
  tone: 'fact' | 'assumption' | 'unknown'
}) {
  return <article className={`knowledge-list knowledge-list-${tone}`}><h3>{title}</h3>
    {values.length > 0 ? <ul>{values.map((value, index) => <li key={`${value}-${index}`}>{value}</li>)}</ul>
      : <p>None recorded.</p>}</article>
}
