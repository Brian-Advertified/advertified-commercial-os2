import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'

type Props = {
  understanding: SuppliedBriefUnderstanding | null
  busy: boolean
}

const extractedFields = [
  'Client or brand',
  'Business problem and objective',
  'Audience and geography',
  'Timing, budget and tax treatment',
  'Media requirements and constraints',
  'Measurement, evidence and unknowns',
] as const

export function BriefIntakeGuide({ understanding, busy }: Props) {
  const state = understanding
    ? understanding.requiresHumanClarification ? 'clarify' : 'review'
    : busy ? 'understand' : 'source'

  return <aside className="brief-intake-guide" aria-label="How Advertified prepares the Brief">
    <div className="brief-guide-heading">
      <span className="brief-guide-mark" aria-hidden="true">AI</span>
      <div><p className="eyebrow eyebrow-light">Brief intelligence</p>
        <h2>From raw request to a planning-ready Brief</h2></div>
    </div>

    <ol className="brief-guide-stages">
      <GuideStage number="1" title="Preserve the original wording"
        detail="The supplied request is carried into the campaign record when you confirm the Brief."
        status={stageStatus(state, 'source')} />
      <GuideStage number="2" title="Understand the campaign"
        detail="Advertified structures the commercial requirements and supporting evidence."
        status={stageStatus(state, 'understand')} />
      <GuideStage number="3" title="Resolve only unclear details"
        detail="You are asked only about information that blocks a reliable plan."
        status={stageStatus(state, 'clarify')} />
      <GuideStage number="4" title="Begin planning"
        detail="The approved Brief becomes the foundation for audience, media and supply decisions."
        status={stageStatus(state, 'review')} />
    </ol>

    <div className="brief-guide-extracts">
      <span>Advertified will identify</span>
      <div>{extractedFields.map((field) =>
        <p key={field}><span aria-hidden="true">✓</span>{field}</p>)}</div>
    </div>

    <p className="brief-guide-note">
      No client record is required before you start. The client can be resolved from the supplied Brief as part of this process.
    </p>
  </aside>
}

function GuideStage({ number, title, detail, status }: {
  number: string
  title: string
  detail: string
  status: 'complete' | 'current' | 'upcoming'
}) {
  return <li className={`is-${status}`} aria-current={status === 'current' ? 'step' : undefined}>
    <span className="brief-guide-stage-number">{status === 'complete' ? '✓' : number}</span>
    <div><strong>{title}</strong><p>{detail}</p></div>
  </li>
}

function stageStatus(
  current: 'source' | 'understand' | 'clarify' | 'review',
  stage: 'source' | 'understand' | 'clarify' | 'review',
): 'complete' | 'current' | 'upcoming' {
  const order = ['source', 'understand', 'clarify', 'review'] as const
  const currentIndex = order.indexOf(current)
  const stageIndex = order.indexOf(stage)
  if (stageIndex < currentIndex) return 'complete'
  return stageIndex === currentIndex ? 'current' : 'upcoming'
}
