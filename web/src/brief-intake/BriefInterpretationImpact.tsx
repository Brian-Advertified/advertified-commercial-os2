import type { SuppliedBriefUnderstanding } from '../api/brief-understanding-schemas'
import { campaignModeLabel } from './brief-intake-presentation'

export function BriefInterpretationImpact({ understanding }: {
  understanding: SuppliedBriefUnderstanding
}) {
  const draft = understanding.draft
  const core = coreInputs(understanding)
  const blocking = understanding.questions.filter(item => item.isBlocking).length
  const clarification = understanding.questions.length
  return <section className="brief-interpretation-impact" aria-labelledby="interpretation-impact-title">
    <header><p className="eyebrow">What Advertified did</p>
      <h2 id="interpretation-impact-title">Turned the client request into a commercial decision</h2>
      <p>{impactSentence(core, understanding.evidence.length, blocking, clarification)}</p></header>
    <div className="brief-impact-flow">
      <article><span>1</span><div><small>Client request</small><strong>{understanding.title}</strong>
        <p>Original wording remains retained as the source.</p></div></article>
      <article><span>2</span><div><small>Advertified structured</small><strong>{core}/8 commercial inputs</strong>
        <p>Problem, outcome, audience, place, timing, budget, media direction and measurement.</p></div></article>
      <article><span>3</span><div><small>Planning decision</small><strong>{campaignModeLabel(understanding.campaignMode)}</strong>
        <p>{blocking === 0 ? 'No blocking clarification remains.' : `${blocking} blocking clarification${blocking === 1 ? '' : 's'} remain.`}</p></div></article>
    </div>
    <div className="brief-impact-facts">
      <span><strong>{draft.facts.length}</strong> retained facts</span>
      <span><strong>{understanding.evidence.length}</strong> evidence links</span>
      <span><strong>{draft.assumptions.length}</strong> explicit assumptions</span>
      <span><strong>{draft.unknowns.length}</strong> unknowns kept visible</span>
    </div>
  </section>
}

function coreInputs(understanding: SuppliedBriefUnderstanding) {
  const draft = understanding.draft
  return [
    draft.businessProblem.trim(), draft.objective.trim(), draft.audiences.length,
    draft.geographies.length, draft.timing.trim(),
    !draft.budgetUnknown && draft.budgetMinor !== null,
    draft.mediaRequirements.length, draft.measurement.length,
  ].filter(Boolean).length
}

function impactSentence(core: number, evidence: number, blocking: number, clarification: number) {
  const decisions = blocking > 0
    ? `${blocking} blocking decision${blocking === 1 ? '' : 's'} still need human judgement`
    : clarification > 0
      ? `${clarification} non-blocking clarification${clarification === 1 ? '' : 's'} remain visible`
      : 'no clarification is required before review'
  return `Advertified surfaced ${core} of 8 core commercial inputs, linked ${evidence} source evidence point${evidence === 1 ? '' : 's'}, and ${decisions}.`
}
