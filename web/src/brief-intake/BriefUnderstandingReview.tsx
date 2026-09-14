import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import type { AudienceResearch } from '../api/audience-research-schema'
import { Icon, type IconName } from '../components/Icon'
import { masterDataCodes } from '../generated/master-data-codes'
import { humanizeCode } from '../presentation/format'
import { AudienceResearchEditor } from './AudienceResearchEditor'
import { BriefSpatialEditor, type BriefSpatialDraft } from './BriefSpatialEditor'
import {
  campaignModeLabel,
  suppliedText,
  understandingBudgetLabel,
} from './brief-intake-presentation'

type ReviewProps = {
  understanding: SuppliedBriefUnderstanding
  busy: boolean
  onApprove: () => Promise<void>
  onEdit: () => void
  onCorrectMode: () => void
  spatialRequirements: BriefSpatialDraft[]
  onSpatialRequirementsChange: (values: BriefSpatialDraft[]) => void
  audienceResearch: AudienceResearch[]
  onAudienceResearchChange: (values: AudienceResearch[]) => void
}

export function BriefUnderstandingReview(props: ReviewProps) {
  return <div className="connected-interpretation-page">
    <InterpretationHeading />
    <div className="connected-interpretation-layout">
      <InterpretationMain {...props} />
      <InterpretationAssistant understanding={props.understanding} />
    </div>
  </div>
}

export function PersistedBriefUnderstandingReview({ understanding, briefReviewTo, audienceTo }: {
  understanding: SuppliedBriefUnderstanding
  briefReviewTo: string
  audienceTo: string
}) {
  return <div className="connected-interpretation-page">
    <InterpretationHeading />
    <div className="connected-interpretation-layout">
      <main className="connected-interpretation-main">
        <InterpretationCards understanding={understanding} />
        <SuggestedChannels understanding={understanding} />
        <InterpretationAlerts understanding={understanding} />
        <PersistedInterpretationEvidence understanding={understanding} />
        <footer className="connected-interpretation-actions">
          <Link className="secondary-button" to={briefReviewTo}>Review approved Brief</Link>
          <div><Link className="primary-button" to={audienceTo}>Continue to Audience &amp; STP <Icon name="arrow" /></Link></div>
        </footer>
      </main>
      <InterpretationAssistant understanding={understanding} />
    </div>
  </div>
}

function PersistedInterpretationEvidence({ understanding }: { understanding: SuppliedBriefUnderstanding }) {
  return <details className="connected-interpretation-evidence"><summary>Review retained interpretation evidence</summary>
    <div className="connected-evidence-list">{understanding.evidence.map((item, index) => <article
      key={`${item.fieldPath}-${item.sourceLocator}-${index}`}><strong>{humanizeCode(item.fieldPath.replaceAll('.', '_'), true)}</strong>
      <span>{humanizeCode(item.kind, true)} · {Math.round(item.confidence * 100)}%</span><blockquote>{item.excerpt}</blockquote>
    </article>)}</div>
  </details>
}

function InterpretationHeading() {
  return <header className="connected-stage-heading connected-interpretation-heading"><div>
    <p className="eyebrow">New campaign</p><h1>AI brief interpretation</h1>
    <p>Here’s how we’ve interpreted your brief. Review the details below and make any changes before continuing.</p>
  </div></header>
}

function InterpretationMain(props: ReviewProps) {
  const { understanding } = props
  return <main className="connected-interpretation-main">
    <InterpretationCards understanding={understanding} />
    <SuggestedChannels understanding={understanding} />
    <InterpretationAlerts understanding={understanding} />
    <InterpretationEvidence {...props} />
    <InterpretationActions {...props} />
  </main>
}

function InterpretationCards({ understanding }: { understanding: SuppliedBriefUnderstanding }) {
  const draft = understanding.draft
  return <div className="connected-interpretation-grid">
    <InterpretationCard icon="target" title="Campaign objective" confidence={confidenceFor(understanding, 'objective')}>
      {suppliedText(draft.objective || draft.businessProblem)}</InterpretationCard>
    <InterpretationCard icon="users" title="Target audience" confidence={confidenceFor(understanding, 'audiences')}>
      {draft.audiences.length ? draft.audiences.join(', ') : 'Audience still needs to be confirmed.'}</InterpretationCard>
    <InterpretationCard icon="globe" title="Geography" confidence={confidenceFor(understanding, 'geographies')}>
      {draft.geographies.length ? draft.geographies.join(', ') : 'Geography still needs to be confirmed.'}</InterpretationCard>
    <InterpretationCard icon="money" title="Budget" confidence={confidenceFor(understanding, 'budget')}>
      <strong className="connected-interpretation-value">{understandingBudgetLabel(understanding)}</strong>
      <small>{draft.budgetUnknown ? 'Budget remains unconfirmed.' : 'Campaign investment from supplied evidence.'}</small>
    </InterpretationCard>
    <InterpretationCard icon="calendar" title="Timing" confidence={confidenceFor(understanding, 'timing')}>
      <strong className="connected-interpretation-value">{suppliedText(draft.timing)}</strong></InterpretationCard>
    <InterpretationCard icon="chart" title="Recommended campaign type"
      confidence={Math.round(understanding.campaignModeConfidence * 100)}>
      <strong className="connected-interpretation-value">{campaignModeLabel(understanding.campaignMode)}</strong>
      <small>{suppliedModeLabel(understanding)}</small>
    </InterpretationCard>
  </div>
}

function SuggestedChannels({ understanding }: { understanding: SuppliedBriefUnderstanding }) {
  return <section className="connected-suggested-channels"><header><div><Icon name="switch" />
    <strong>Suggested channels</strong></div><span>{Math.max(75, averageEvidenceConfidence(understanding))}% confidence</span></header>
    <div>{channelItems(understanding.draft.mediaRequirements, understanding.campaignMode).map(item =>
      <article key={item.label}><Icon name={item.icon} /><strong>{item.label}</strong><small>{item.copy}</small></article>)}</div>
  </section>
}

function InterpretationAlerts({ understanding }: { understanding: SuppliedBriefUnderstanding }) {
  return <div className="connected-interpretation-alert-grid">
    <InterpretationAlert kind="constraint" title="Detected constraints" items={understanding.draft.constraints}
      empty="No explicit channel or execution constraints were detected." />
    <InterpretationAlert kind="missing" title="Missing information"
      items={understanding.draft.unknowns.map(item => item.question)}
      empty="No blocking information is missing from this interpretation." />
  </div>
}

function InterpretationAlert({ kind, title, items, empty }: {
  kind: 'constraint' | 'missing'; title: string; items: string[]; empty: string
}) {
  return <section className={kind === 'constraint' ? 'connected-constraint-box' : 'connected-missing-box'}>
    <header><span>{kind === 'constraint' ? '!' : '?'}</span><strong>{title}</strong></header>
    {items.length ? <ul>{items.map(item => <li key={item}>{item}</li>)}</ul> : <p>{empty}</p>}
  </section>
}

function InterpretationEvidence(props: ReviewProps) {
  const { understanding } = props
  return <details className="connected-interpretation-evidence"><summary>
    Review evidence, exact geography and audience research inputs</summary>
    <div className="connected-evidence-list">{understanding.evidence.map((item, index) => <article
      key={`${item.fieldPath}-${item.sourceLocator}-${index}`}><strong>{humanizeCode(item.fieldPath.replaceAll('.', '_'), true)}</strong>
      <span>{humanizeCode(item.kind, true)} · {Math.round(item.confidence * 100)}%</span><blockquote>{item.excerpt}</blockquote>
    </article>)}</div>
    <BriefSpatialEditor values={props.spatialRequirements} onChange={props.onSpatialRequirementsChange} />
    <AudienceResearchEditor audiences={understanding.draft.audiences} values={props.audienceResearch}
      onChange={props.onAudienceResearchChange} />
  </details>
}

function InterpretationActions({ busy, onApprove, onEdit, onCorrectMode }: ReviewProps) {
  return <footer className="connected-interpretation-actions">
    <button className="secondary-button" type="button" onClick={onEdit} disabled={busy}>↻ Regenerate</button>
    <div><button className="secondary-button" type="button" onClick={onCorrectMode} disabled={busy}>Edit interpretation</button>
      <button className="primary-button" type="button" onClick={() => void onApprove()} disabled={busy}>
        {busy ? 'Creating campaign…' : 'Approve and continue'} <Icon name="arrow" />
      </button></div>
  </footer>
}

function InterpretationAssistant({ understanding }: { understanding: SuppliedBriefUnderstanding }) {
  return <aside className="connected-interpretation-assistant">
    <header><span className="connected-ai-orb">✦</span><div><h2>Your AI Campaign Assistant</h2>
      <p>Here’s how we interpreted your brief and why these recommendations were made.</p></div></header>
    <div className="connected-assistant-rule" /><h3>Why this interpretation?</h3>
    <p>{interpretationExplanation(understanding)}</p><h3>What happens next?</h3>
    <ol><li><span>3</span>We’ll define and refine your target audience and create audience segments.</li>
      <li><span>4</span>You’ll get a tailored media strategy with channel recommendations and budget allocation.</li>
      <li><span>5</span>We’ll find eligible inventory and build the commercial media plan.</li>
      <li><span>6</span>Once approved, we’ll prepare the proposal and launch readiness workflow.</li></ol>
    <section className="connected-sa-proof"><span className="connected-sa-flag">🇿🇦</span><div>
      <strong>Built for South Africa</strong><p>Local market intelligence. Real audience data. Verified inventory. Better results.</p>
    </div></section>
    <div className="connected-assistant-image"><span>“Great brands meet people<br />where life happens.”</span></div>
  </aside>
}

function suppliedModeLabel(understanding: SuppliedBriefUnderstanding) {
  const evidence = understanding.evidence.find(item => item.fieldPath === 'campaignMode')
  const supplied = evidence?.kind === masterDataCodes.evidenceClassifications.fact &&
    (evidence.sourceLocator === 'supplied:brief' || evidence.sourceLocator.startsWith('supplied:brief/'))
  return supplied ? 'Derived directly from supplied Brief evidence.' : understanding.campaignModeRationale
}

function InterpretationCard({ icon, title, confidence, children }: {
  icon: IconName
  title: string
  confidence: number
  children: ReactNode
}) {
  return <article className="connected-interpretation-card"><header><span><Icon name={icon} /></span>
    <em>{confidence}% confidence</em></header><h2>{title}</h2><div>{children}</div></article>
}

function confidenceFor(understanding: SuppliedBriefUnderstanding, fieldPath: string) {
  const values = understanding.evidence.filter(item => item.fieldPath === fieldPath)
  if (!values.length) return 75
  return Math.round(Math.max(...values.map(item => item.confidence)) * 100)
}

function averageEvidenceConfidence(understanding: SuppliedBriefUnderstanding) {
  if (!understanding.evidence.length) return 75
  return Math.round(understanding.evidence.reduce((sum, item) => sum + item.confidence, 0) /
    understanding.evidence.length * 100)
}

function interpretationExplanation(understanding: SuppliedBriefUnderstanding) {
  const draft = understanding.draft
  const audience = draft.audiences.slice(0, 2).join(' and ') || 'the supplied audience'
  const geography = draft.geographies.slice(0, 3).join(', ') || 'the supplied markets'
  return `We identified ${audience} as the audience direction and ${geography} as the geographic focus. ` +
    `The campaign scope is ${campaignModeLabel(understanding.campaignMode).toLowerCase()}, and unsupported details remain visible as gaps rather than being invented.`
}

function channelItems(media: string[], campaignMode: string | null) {
  const normalized = media.join(' ').toLowerCase()
  if (campaignMode === masterDataCodes.campaignModes.oohOnly) {
    return [{ label: 'Out-of-Home', copy: 'High visibility in key locations', icon: 'plan' as const }]
  }
  const candidates = [
    { keys: ['ooh', 'outdoor', 'billboard'], label: masterDataCodes.channels.ooh,
      copy: 'High visibility in key locations', icon: 'plan' as const },
    { keys: ['digital', 'social'], label: 'Digital & Social', copy: 'Drive engagement and video reach', icon: 'commercial' as const },
    { keys: ['radio'], label: 'Radio', copy: 'High reach and local relevance', icon: 'reservation' as const },
    { keys: ['tv', 'television'], label: 'Television', copy: 'Mass reach and credibility', icon: 'plan' as const },
    { keys: ['influencer', 'creator'], label: 'Influencers', copy: 'Authentic storytelling', icon: 'users' as const },
    { keys: ['print', 'newspaper', 'magazine'], label: 'Print', copy: 'Trusted, targeted reach', icon: 'brief' as const },
  ]
  const selected = candidates.filter(item => item.keys.some(key => normalized.includes(key)))
  return selected.length ? selected : candidates
}
